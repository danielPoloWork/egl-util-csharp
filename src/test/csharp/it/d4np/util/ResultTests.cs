using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class ResultTests
{
    [Fact]
    public void Success_is_successful_with_no_errors()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Errors);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_carries_its_errors_in_order()
    {
        var first = new Error("E1", "first");
        var second = new Error("E2", "second");

        var result = Result.Failure(first, second);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(new[] { first, second }, result.Errors);
        Assert.Equal(first, result.Error);
    }

    [Fact]
    public void Failure_requires_at_least_one_error()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure());
    }

    [Fact]
    public void Failure_rejects_a_null_error_array()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }
}
