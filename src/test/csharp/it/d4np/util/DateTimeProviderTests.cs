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
}
