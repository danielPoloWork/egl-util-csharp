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
    /// Publishes the pending domain events of each tracked aggregate through <paramref name="publish"/>,
    /// then clears that aggregate's events <b>on success</b>. Call this <b>after</b> a committed
    /// <c>SaveChangesAsync</c> so events are never published for a rolled-back unit of work.
    /// </summary>
    /// <remarks>
    /// Delivery is <b>at-least-once</b>: events publish in change-tracking order (then raise-order
    /// within an aggregate), and an aggregate's buffer is cleared only once all of its events have
    /// been published. If <paramref name="publish"/> throws, the failing aggregate and every aggregate
    /// after it keep their events buffered, so a retry re-drives only the undelivered work
    /// (already-cleared aggregates are not re-published).
    /// </remarks>
    /// <param name="context">The EF Core context to scan.</param>
    /// <param name="publish">The callback that publishes a single domain event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once every pending event has been published and cleared.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="publish"/> is <see langword="null"/>.</exception>
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
}
