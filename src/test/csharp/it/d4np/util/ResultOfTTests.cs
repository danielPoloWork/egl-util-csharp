using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class ResultOfTTests
{
    [Fact]
    public void Success_exposes_its_value()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Accessing_the_value_of_a_failure_throws()
    {
        Result<int> result = Result.Failure<int>(new Error("E", "m"));

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Implicitly_converts_from_a_value_to_success()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void Implicitly_converts_from_an_error_to_failure()
    {
        Result<string> result = new Error("User.NotFound", "No such user.");

        Assert.True(result.IsFailure);
        Assert.Equal("User.NotFound", result.Error.Code);
    }

    [Fact]
    public void Success_rejects_a_null_value()
    {
        // Regression for #27: the type's premise is "no nulls". A null success would defer a
        // NullReferenceException to a downstream Value read even though Value is typed non-nullable.
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Success<int?>(null));

        // ...including via the implicit value -> Result<TValue> conversion. A typed null reference (not
        // the null literal, which would bind to the reference type and bypass the operator) exercises it.
        string? nullValue = null;
        Assert.Throws<ArgumentNullException>(() => _ = (Result<string>)nullValue!);
    }
}
