using Xunit;

namespace It.D4np.Util.Tests;

/// <summary>
/// Milestone-1 smoke test: proves the test harness runs and the seeded version constant is
/// in lockstep with the README badge / consistency_lint expectation (0.0.0 at bootstrap).
/// </summary>
public class LibraryVersionTests
{
    [Fact]
    public void Version_is_seeded_at_zero()
    {
        Assert.Equal("0.0.0", LibraryVersion.Version);
    }
}
