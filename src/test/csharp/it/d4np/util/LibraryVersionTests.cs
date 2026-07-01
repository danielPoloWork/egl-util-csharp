using Xunit;

namespace It.D4np.Util.Tests;

/// <summary>
/// Proves the library version constant is the current release and is kept in lockstep with the
/// README badge, the package version, and the release files by <c>tools/consistency_lint.py</c>.
/// </summary>
public class LibraryVersionTests
{
    [Fact]
    public void Version_is_the_current_release()
    {
        Assert.Equal("1.0.0", LibraryVersion.Version);
    }
}
