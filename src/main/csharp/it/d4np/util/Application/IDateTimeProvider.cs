namespace It.D4np.Util.Application;

/// <summary>
/// An injectable abstraction over the system clock. Code that needs the current time depends on this
/// contract rather than <see cref="DateTime.UtcNow"/> directly, so time is deterministic and
/// controllable in tests (substitute a fake) and event timestamps are reproducible.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// The current date and time in UTC. Implementations must return a value whose
    /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>, so it is safe to stamp directly
    /// onto <c>IDomainEvent.OccurredOnUtc</c> and to convert without a hidden local-offset shift.
    /// </summary>
    DateTime UtcNow { get; }
}
