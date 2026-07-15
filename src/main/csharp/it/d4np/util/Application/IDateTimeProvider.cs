namespace It.D4np.Util.Application;

/// <summary>
/// An injectable abstraction over the system clock. Code that needs the current time depends on this
/// contract rather than <see cref="DateTime.UtcNow"/> directly, so time is deterministic and
/// controllable in tests (substitute a fake) and event timestamps are reproducible.
/// </summary>
/// <remarks>
/// On .NET 8+ this is a thin facade over the BCL's <see cref="System.TimeProvider"/>: the default
/// implementation delegates to an injected <see cref="System.TimeProvider"/> (and <c>AddD4npUtil</c>
/// registers <see cref="System.TimeProvider.System"/>). For new code that needs more than UtcNow —
/// <see cref="System.DateTimeOffset"/>, high-resolution timestamps, or timers — prefer injecting
/// <see cref="System.TimeProvider"/> directly (with <c>FakeTimeProvider</c> in tests).
/// </remarks>
public interface IDateTimeProvider
{
    /// <summary>
    /// The current date and time in UTC. Implementations must return a value whose
    /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>, so it is safe to stamp directly
    /// onto <c>IDomainEvent.OccurredOnUtc</c> and to convert without a hidden local-offset shift.
    /// </summary>
    DateTime UtcNow { get; }
}
