using It.D4np.Util.Application;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// The default <see cref="IDateTimeProvider"/>, returning the real system clock in UTC
/// (<see cref="DateTime.UtcNow"/>). This is the single sanctioned use of the ambient clock; all other
/// code takes an <see cref="IDateTimeProvider"/>. Register it as a singleton and substitute a fake in tests.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
