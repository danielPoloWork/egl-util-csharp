namespace It.D4np.Util.Domain;

/// <summary>
/// Non-generic marker for aggregate roots that buffer domain events, so infrastructure can
/// enumerate and dispatch the events of every tracked aggregate regardless of its identity type.
/// Implemented by <see cref="AggregateRoot{TId}"/>.
/// </summary>
/// <remarks>
/// <b>Threading.</b> An implementation buffers events in unsynchronized state, and an aggregate is
/// confined to a single unit of work: reading <see cref="DomainEvents"/> or calling
/// <see cref="ClearDomainEvents"/> concurrently with the domain code raising events on the same
/// aggregate is a data race. See <see cref="AggregateRoot{TId}"/> for the full confinement contract.
/// </remarks>
public interface IHasDomainEvents
{
    /// <summary>The domain events raised on this aggregate and not yet dispatched, in the order raised.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Clears all buffered domain events. Called by the dispatcher after delivery.</summary>
    void ClearDomainEvents();
}
