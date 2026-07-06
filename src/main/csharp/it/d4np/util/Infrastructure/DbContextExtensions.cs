using It.D4np.Util.Domain;

using Microsoft.EntityFrameworkCore;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Extensions that bridge the domain's <see cref="IDomainEvent"/>s to a persistence transaction:
/// inspect, clear, or dispatch the events buffered on tracked aggregate roots around
/// <c>SaveChanges</c>, without coupling the context to a specific message bus.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Collects — <b>without</b> clearing — the domain events buffered on every tracked aggregate
    /// root, in change-tracking order (then raise-order within each aggregate).
    /// </summary>
    /// <param name="context">The EF Core context to scan.</param>
    /// <returns>The buffered domain events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<IDomainEvent> PeekDomainEvents(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();
    }

    /// <summary>Clears the buffered domain events on every tracked aggregate root.</summary>
    /// <param name="context">The EF Core context to scan.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static void ClearDomainEvents(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (IHasDomainEvents aggregate in context.ChangeTracker.Entries<IHasDomainEvents>().Select(entry => entry.Entity))
        {
            aggregate.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Publishes the pending domain events of each <b>currently tracked</b> aggregate through
    /// <paramref name="publish"/>, then clears that aggregate's events <b>on success</b>. Call this
    /// <b>after</b> a committed <c>SaveChangesAsync</c> so events are never published for a rolled-back
    /// unit of work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Delivery is <b>at-least-once</b>: events publish in change-tracking order (then raise-order
    /// within an aggregate), and an aggregate's buffer is cleared only once all of its events have
    /// been published. If <paramref name="publish"/> throws, the failing aggregate and every aggregate
    /// after it keep their events buffered, so a retry re-drives only the undelivered work
    /// (already-cleared aggregates are not re-published).
    /// </para>
    /// <para>
    /// <b>Limitation — events on removed aggregates are lost.</b> This overload re-scans the change
    /// tracker, so it only sees aggregates that are still tracked <i>after</i> the save. EF Core
    /// detaches <c>Deleted</c> entities from the change tracker once <c>SaveChanges</c> completes, so
    /// an aggregate that raised an event and was then <c>Remove</c>d (e.g. <c>order.Cancel()</c> then
    /// <c>repository.Remove(order)</c>) is gone by the time this runs and its events are silently
    /// dropped. To publish events for removed aggregates, capture a snapshot <i>before</i> saving with
    /// <see cref="CaptureDomainEvents(DbContext)"/> and dispatch that instead.
    /// </para>
    /// </remarks>
    /// <param name="context">The EF Core context to scan.</param>
    /// <param name="publish">The callback that publishes a single domain event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once every pending event has been published and cleared.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="publish"/> is <see langword="null"/>.</exception>
    /// <seealso cref="CaptureDomainEvents(DbContext)"/>
    public static async Task DispatchDomainEventsAsync(
        this DbContext context,
        Func<IDomainEvent, CancellationToken, Task> publish,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(publish);

        IHasDomainEvents[] aggregates = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToArray();

        foreach (IHasDomainEvents aggregate in aggregates)
        {
            foreach (IDomainEvent domainEvent in aggregate.DomainEvents.ToArray())
            {
                await publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }

            // Clear only after all of this aggregate's events were published (clear-on-success),
            // so a publish failure leaves undelivered events buffered for a retry.
            aggregate.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Captures — <b>without</b> clearing — a snapshot of the domain events buffered on every tracked
    /// aggregate root, so they can be published <b>after</b> a committed <c>SaveChangesAsync</c> even
    /// for aggregates the save removes. Take the snapshot <b>before</b> calling <c>SaveChangesAsync</c>,
    /// then dispatch it with
    /// <see cref="DispatchDomainEventsAsync(DomainEventSnapshot, Func{IDomainEvent, CancellationToken, Task}, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// This is the safe counterpart to the change-tracker overload for units of work that delete
    /// aggregates: EF Core detaches <c>Deleted</c> entities from the change tracker once the save
    /// completes, so a post-save re-scan would silently miss their events. The returned snapshot holds
    /// a strong reference to each aggregate plus a copy of its events, so nothing is lost across the save.
    /// </remarks>
    /// <param name="context">The EF Core context to scan.</param>
    /// <returns>A snapshot of the pending domain events, in change-tracking order (then raise-order within each aggregate).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static DomainEventSnapshot CaptureDomainEvents(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        CapturedAggregateEvents[] captured = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .Select(aggregate => new CapturedAggregateEvents(aggregate, aggregate.DomainEvents.ToArray()))
            .ToArray();

        return new DomainEventSnapshot(captured);
    }

    /// <summary>
    /// Publishes the domain events captured in <paramref name="snapshot"/> through
    /// <paramref name="publish"/>, clearing each source aggregate's buffer <b>on success</b>. Call this
    /// <b>after</b> a committed <c>SaveChangesAsync</c> so events are never published for a rolled-back
    /// unit of work — while still delivering events for aggregates the save removed (which
    /// <see cref="CaptureDomainEvents(DbContext)"/> captured before the save).
    /// </summary>
    /// <remarks>
    /// Delivery is <b>at-least-once</b>, matching the change-tracker overload: events publish in the
    /// captured order, and an aggregate's buffer is cleared only once all of its captured events have
    /// been published. If <paramref name="publish"/> throws, the failing aggregate and every aggregate
    /// after it stay undelivered, so re-dispatching the <b>same</b> snapshot re-drives only the
    /// outstanding work (already-cleared aggregates are not re-published).
    /// </remarks>
    /// <param name="snapshot">The snapshot captured before the save via <see cref="CaptureDomainEvents(DbContext)"/>.</param>
    /// <param name="publish">The callback that publishes a single domain event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once every captured event has been published and cleared.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> or <paramref name="publish"/> is <see langword="null"/>.</exception>
    public static async Task DispatchDomainEventsAsync(
        this DomainEventSnapshot snapshot,
        Func<IDomainEvent, CancellationToken, Task> publish,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(publish);

        while (snapshot.Pending.Count > 0)
        {
            CapturedAggregateEvents captured = snapshot.Pending[0];
            foreach (IDomainEvent domainEvent in captured.Events)
            {
                await publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }

            // Clear only after all of this aggregate's captured events were published, then drop it
            // from the snapshot so a re-dispatch re-drives only the still-undelivered aggregates.
            captured.Aggregate.ClearDomainEvents();
            snapshot.Pending.RemoveAt(0);
        }
    }
}
