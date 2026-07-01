using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class DateTimeProviderTests
{
    [Fact]
    public void UtcNow_returns_a_utc_time_within_the_surrounding_system_readings()
    {
        IDateTimeProvider provider = new DateTimeProvider();

        DateTime before = DateTime.UtcNow;
        DateTime value = provider.UtcNow;
        DateTime after = DateTime.UtcNow;

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.InRange(value, before, after);
    }
}
