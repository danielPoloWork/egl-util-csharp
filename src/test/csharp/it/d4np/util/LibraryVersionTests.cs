using Xunit;

namespace It.D4np.Util.Tests;

/// <summary>
/// Proves the library version reports the current release, resolved at runtime from the loaded assembly,
/// and is kept in lockstep with the README badge and the release files by <c>tools/consistency_lint.py</c>.
/// </summary>
public class LibraryVersionTests
{
    [Fact]
    public void Version_is_the_current_release()
    {
        Assert.Equal("1.0.0", LibraryVersion.Version);
    }

    [Fact]
    public void Version_is_a_runtime_resolved_property_not_a_compile_time_const()
    {
        // #86: a public const is inlined into a consumer at ITS compile time, so an in-place library
        // upgrade (shared runtime, plugin host, transitive bump) leaves the consumer reporting the old
        // version. Version must be a property resolved at runtime from the loaded assembly — not a const
        // (literal) field. Guards against a regression to `public const`.
        Assert.NotNull(typeof(LibraryVersion).GetProperty(nameof(LibraryVersion.Version)));
        Assert.Null(typeof(LibraryVersion).GetField(nameof(LibraryVersion.Version)));
    }
}
