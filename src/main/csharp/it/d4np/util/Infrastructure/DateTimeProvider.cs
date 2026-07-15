using It.D4np.Util.Application;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// The default <see cref="IDateTimeProvider"/>, delegating to a <see cref="System.TimeProvider"/> and
/// returning its time in UTC. On .NET 8+ this bridges the library's clock facade to the BCL's
/// <see cref="TimeProvider"/>: <c>AddD4npUtil</c> registers <see cref="TimeProvider.System"/>, which is
/// injected here, so a consumer can also inject <see cref="TimeProvider"/> directly and both clocks agree.
/// Substitute a fake (e.g. <c>FakeTimeProvider</c>) in tests. All other code takes an <see cref="IDateTimeProvider"/>.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a provider over <paramref name="timeProvider"/>, or <see cref="TimeProvider.System"/> when it is null.</summary>
    /// <param name="timeProvider">The clock to read; defaults to <see cref="TimeProvider.System"/>.</param>
    public DateTimeProvider(TimeProvider? timeProvider = null) => _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;
}
