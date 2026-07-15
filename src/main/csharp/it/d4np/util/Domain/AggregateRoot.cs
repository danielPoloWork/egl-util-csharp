namespace It.D4np.Util.Domain;

/// <summary>
/// Base class for aggregate roots — the single entry point to a consistency boundary. An
/// aggregate root is an <see cref="Entity{TId, TEntity}"/> that buffers the <see cref="IDomainEvent"/>s
/// raised during a unit of work until an infrastructure boundary collects, dispatches, and
/// clears them.
/// </summary>
/// <remarks>
/// <b>Threading.</b> The domain-event buffer is an ordinary, <b>unsynchronized</b> list, so an
/// aggregate instance is <b>not</b> safe for concurrent use: <see cref="RaiseDomainEvent"/>,
/// <see cref="DomainEvents"/>, and <see cref="ClearDomainEvents"/> all read or mutate it without
/// locking. The contract is <b>single-thread confinement to one unit of work</b> — the aggregate is
/// created, mutated, persisted, and has its events dispatched-and-cleared on a single logical flow
/// (typically the thread owning that unit of work's EF Core <c>DbContext</c>, which is itself not
/// thread-safe), so the domain code that raises events and the infrastructure boundary that enumerates
/// and clears them never touch the buffer concurrently. Mutating one aggregate from multiple threads —
/// or having one thread raise an event while another concurrently enumerates or clears the buffer — is
/// a data race the library does not guard against; confine each aggregate to a single flow at a time.
/// A <i>re-entrant</i> raise on that same flow (e.g. a handler that raises another event while the
/// aggregate is being dispatched) is not concurrent access and is safe.
/// </remarks>
/// <typeparam name="TId">The strongly-typed aggregate identifier; must be non-nullable.</typeparam>
/// <typeparam name="TAggregate">The canonical aggregate type (the concrete type closes the generic over itself).</typeparam>
public abstract class AggregateRoot<TId, TAggregate> : Entity<TId, TAggregate>, IHasDomainEvents
    where TId : notnull
    where TAggregate : AggregateRoot<TId, TAggregate>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Initializes a new aggregate root with the given identity.</summary>
    /// <param name="id">The aggregate identity; must not be <see langword="null"/>.</param>
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>
    /// The domain events raised on this aggregate and not yet dispatched, in the order raised.
    /// </summary>
    /// <remarks>
    /// This is a <b>live read-only view</b> over the internal buffer, not a snapshot: it reflects later
    /// <see cref="RaiseDomainEvent"/> calls and is emptied by <see cref="ClearDomainEvents"/>. Two consequences
    /// for a single-threaded caller: a reference captured <em>before</em> dispatch (for an audit trail,
    /// logging, or assertions) reads back <b>empty</b> once the dispatcher clears the aggregate — it was never
    /// a snapshot; and enumerating it directly in a dispatch loop that lets a handler raise a follow-up event
    /// on the same aggregate throws <see cref="InvalidOperationException"/> ("Collection was modified").
    /// <b>Materialize a copy</b> (<c>ToArray()</c>/<c>ToList()</c>) before a dispatch loop or before retaining
    /// the events; the library's own <c>DispatchDomainEventsAsync</c> does exactly that. (Distinct from the
    /// threading contract in the type remarks; both concern single-thread use.) Each access also allocates a
    /// fresh read-only wrapper.
    /// </remarks>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Records a domain event to be dispatched after the aggregate is persisted.</summary>
    /// <param name="domainEvent">The event to record; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Clears all buffered domain events. Called by the dispatcher after delivery.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
