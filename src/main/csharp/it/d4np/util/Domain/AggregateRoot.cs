namespace It.D4np.Util.Domain;

/// <summary>
/// Base class for aggregate roots — the single entry point to a consistency boundary. An
/// aggregate root is an <see cref="Entity{TId}"/> that buffers the <see cref="IDomainEvent"/>s
/// raised during a unit of work until an infrastructure boundary collects, dispatches, and
/// clears them.
/// </summary>
/// <typeparam name="TId">The strongly-typed aggregate identifier; must be non-nullable.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
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
