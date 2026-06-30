using FsCheck.Xunit;

using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

/// <summary>
/// Property tests asserting that <see cref="Result.Combine(Result[])"/> forms a monoid over error
/// aggregation: <see cref="Result.Success()"/> is the identity element and combination is
/// associative, across many randomly generated success/failure inputs.
/// </summary>
public class ResultCombineProperties
{
    // Deterministically derive a result from a seed: even -> success, odd -> a single-error failure.
    private static Result FromSeed(int seed) =>
        seed % 2 == 0
            ? Result.Success()
            : Result.Failure(new Error("E" + seed, "message " + seed));

    [Fact]
    public void Combine_with_no_arguments_is_success()
    {
        Result combined = Result.Combine();

        Assert.True(combined.IsSuccess);
        Assert.Empty(combined.Errors);
    }

    [Fact]
    public void Combine_collects_every_error_in_order()
    {
        var first = new Error("E1", "1");
        var second = new Error("E2", "2");

        Result combined = Result.Combine(Result.Success(), Result.Failure(first), Result.Failure(second));

        Assert.True(combined.IsFailure);
        Assert.Equal(new[] { first, second }, combined.Errors);
    }

    [Property]
    public void Success_is_the_left_identity(int seed)
    {
        Result result = FromSeed(seed);

        Result combined = Result.Combine(Result.Success(), result);

        Assert.Equal(result.IsSuccess, combined.IsSuccess);
        Assert.Equal(result.Errors, combined.Errors);
    }

    [Property]
    public void Success_is_the_right_identity(int seed)
    {
        Result result = FromSeed(seed);

        Result combined = Result.Combine(result, Result.Success());

        Assert.Equal(result.IsSuccess, combined.IsSuccess);
        Assert.Equal(result.Errors, combined.Errors);
    }

    [Property]
    public void Combine_is_associative(int a, int b, int c)
    {
        Result ra = FromSeed(a);
        Result rb = FromSeed(b);
        Result rc = FromSeed(c);

        Result left = Result.Combine(Result.Combine(ra, rb), rc);
        Result right = Result.Combine(ra, Result.Combine(rb, rc));

        Assert.Equal(left.IsSuccess, right.IsSuccess);
        Assert.Equal(left.Errors, right.Errors);
    }
}
