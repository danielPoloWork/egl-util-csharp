namespace It.D4np.Util.Domain;

/// <summary>
/// Marker interface for domain events — an immutable record of something significant that
/// happened in the domain. Aggregates raise events through
/// <see cref="AggregateRoot{TId, TAggregate}.RaiseDomainEvent"/>; an infrastructure boundary later collects
/// and dispatches them.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// The UTC instant at which the event occurred. Stamped by the code that raises the event
    /// (typically from an injected time provider) so that event time is deterministic in tests.
    /// </summary>
    /// <remarks>
    /// This is a plain <see cref="DateTime"/>, so its UTC-ness is a <b>contract the type cannot
    /// enforce</b>. An implementation <b>must</b> return a value whose <see cref="DateTime.Kind"/> is
    /// <see cref="DateTimeKind.Utc"/>: stamp it from a UTC clock — the library's
    /// <c>IDateTimeProvider.UtcNow</c> (preferred, for testability) or <see cref="DateTime.UtcNow"/> —
    /// never <see cref="DateTime.Now"/> (which is <see cref="DateTimeKind.Local"/>). A non-UTC value is
    /// silently wrong: <see cref="DateTime.ToUniversalTime"/> re-bases a <see cref="DateTimeKind.Local"/>
    /// or <see cref="DateTimeKind.Unspecified"/> value by the machine's UTC offset (it is a no-op only on
    /// a true <see cref="DateTimeKind.Utc"/> value). Mind the serialization round trip these events
    /// routinely take (JSON, an outbox, EF Core): <c>System.Text.Json</c> produces
    /// <see cref="DateTimeKind.Unspecified"/> when the text has no <c>Z</c>/offset suffix, and EF Core
    /// value readers default to <see cref="DateTimeKind.Unspecified"/> as well — so a value that truly
    /// holds UTC comes back mis-kinded and the next <see cref="DateTime.ToUniversalTime"/> shifts it. A
    /// deserializer or value converter <b>must</b> normalize the kind back to UTC (e.g.
    /// <c>DateTime.SpecifyKind(value, DateTimeKind.Utc)</c> once the value is known to hold UTC).
    /// Switching this member to <see cref="DateTimeOffset"/> would remove the ambiguity but is a source-
    /// and binary-breaking change, possible only in a future major version.
    /// </remarks>
    DateTime OccurredOnUtc { get; }
}
