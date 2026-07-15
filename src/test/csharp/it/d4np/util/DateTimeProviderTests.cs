using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class DateTimeProviderTests
{
    [Fact]
    public void UtcNow_returns_the_current_utc_time()
    {
        IDateTimeProvider provider = new DateTimeProvider();

        DateTime systemNow = DateTime.UtcNow;
        DateTime value = provider.UtcNow;

        Assert.Equal(DateTimeKind.Utc, value.Kind);

        // Tolerance-based, not strict before<=value<=after bracketing: the wall clock is not monotonic, so
        // an NTP step/slew landing between two consecutive reads would flake a strict range (#104). A
        // minute is orders of magnitude larger than the sub-millisecond gap between these reads, yet far
        // smaller than a wrong-source bug (local time, epoch, or a constant), so it stays a real check.
        Assert.True(
            (value - systemNow).Duration() < TimeSpan.FromMinutes(1),
            $"provider.UtcNow ({value:O}) is not within a minute of the system UTC clock ({systemNow:O}).");
    }

    [Fact]
    public void Delegates_to_the_injected_TimeProvider()
    {
        // #97: DateTimeProvider is a facade over TimeProvider — its UtcNow is the injected clock's time,
        // in UTC. A controllable TimeProvider makes this deterministic (no FakeTimeProvider dependency).
        var fixedUtc = new DateTimeOffset(2026, 7, 15, 8, 30, 0, TimeSpan.Zero);
        IDateTimeProvider provider = new DateTimeProvider(new FixedTimeProvider(fixedUtc));

        Assert.Equal(fixedUtc.UtcDateTime, provider.UtcNow);
        Assert.Equal(DateTimeKind.Utc, provider.UtcNow.Kind);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
