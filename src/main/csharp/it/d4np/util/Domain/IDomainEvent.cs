namespace It.D4np.Util.Domain;

/// <summary>
/// Marker interface for domain events — an immutable record of something significant that
/// happened in the domain. Aggregates raise events through
/// <see cref="AggregateRoot{TId}.RaiseDomainEvent"/>; an infrastructure boundary later collects
/// and dispatches them.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The UTC instant at which the event occurred. Stamped by the code that raises the event
    /// (typically from an injected time provider) so that event time is deterministic in tests.
    /// </summary>
    DateTime OccurredOnUtc { get; }
}
