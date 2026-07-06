using It.D4np.Util.Domain;

using Microsoft.EntityFrameworkCore;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// A point-in-time capture of the domain events buffered on tracked aggregate roots — taken via
/// <see cref="DbContextExtensions.CaptureDomainEvents(DbContext)"/>, typically immediately before
/// <c>SaveChangesAsync</c>.
/// <para>
/// The snapshot holds a strong reference to each aggregate together with a copy of the events it
/// had raised, so those events survive even when the owning aggregate is <b>removed</b> by the save
/// and detached from the change tracker — the case the post-save change-tracker dispatch
/// (<see cref="DbContextExtensions.DispatchDomainEventsAsync(DbContext, Func{IDomainEvent, CancellationToken, Task}, CancellationToken)"/>)
/// silently misses. Publish it after a committed save with
/// <see cref="DbContextExtensions.DispatchDomainEventsAsync(DomainEventSnapshot, Func{IDomainEvent, CancellationToken, Task}, CancellationToken)"/>.
/// </para>
/// <para>
/// The snapshot is <b>drained as it is dispatched</b>: a successful dispatch clears the referenced
/// aggregates' buffers and consumes the snapshot's pending work, so re-dispatching after a failure
/// re-drives only the outstanding aggregates (and a fully-dispatched snapshot dispatches nothing).
/// Only <see cref="Events"/> is stable across dispatch. Dispatch a given snapshot from one caller at
/// a time — it is not safe to dispatch the same instance concurrently.
/// </para>
/// </summary>
public sealed class DomainEventSnapshot
{
    private readonly List<CapturedAggregateEvents> _pending;

    internal DomainEventSnapshot(IReadOnlyList<CapturedAggregateEvents> captured)
    {
        _pending = [.. captured];
        Events = captured.SelectMany(entry => entry.Events).ToArray();
    }

    /// <summary>
    /// The domain events captured in this snapshot, in change-tracking order (then raise-order
    /// within each aggregate). This reflects the original capture and does <b>not</b> shrink as the
    /// snapshot is dispatched, so it is stable for logging and assertions.
    /// </summary>
    public IReadOnlyList<IDomainEvent> Events { get; }

    /// <summary>
    /// The aggregates whose captured events have not yet been published-and-cleared. Drained by
    /// <see cref="DbContextExtensions.DispatchDomainEventsAsync(DomainEventSnapshot, Func{IDomainEvent, CancellationToken, Task}, CancellationToken)"/>
    /// so a re-dispatch after a failure re-drives only the outstanding aggregates.
    /// </summary>
    internal List<CapturedAggregateEvents> Pending => _pending;
}

/// <summary>An aggregate captured with a copy of the events it had raised at snapshot time.</summary>
/// <param name="Aggregate">The aggregate whose buffer is cleared once its captured events are published.</param>
/// <param name="Events">The events copied from the aggregate at capture time.</param>
internal sealed record CapturedAggregateEvents(IHasDomainEvents Aggregate, IReadOnlyList<IDomainEvent> Events);
