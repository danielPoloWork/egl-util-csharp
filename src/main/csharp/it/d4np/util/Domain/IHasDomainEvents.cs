namespace It.D4np.Util.Domain;

/// <summary>
/// Non-generic marker for aggregate roots that buffer domain events, so infrastructure can
/// enumerate and dispatch the events of every tracked aggregate regardless of its identity type.
/// Implemented by <see cref="AggregateRoot{TId}"/>.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>The domain events raised on this aggregate and not yet dispatched, in the order raised.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Clears all buffered domain events. Called by the dispatcher after delivery.</summary>
    void ClearDomainEvents();
}
