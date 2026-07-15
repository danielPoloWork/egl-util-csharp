using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class ResultCompositionTests
{
    private static readonly Error SomeError = new("Test.Error", "boom");

    // --- Result<TValue>.Match ---

    [Fact]
    public void Match_maps_the_value_on_success()
    {
        Result<int> result = Result.Success(21);

        Assert.Equal(42, result.Match(value => value * 2, _ => -1));
    }

    [Fact]
    public void Match_maps_the_errors_on_failure()
    {
        Result<int> result = Result.Failure<int>(SomeError);

        Assert.Equal("boom", result.Match(_ => "ok", errors => errors[0].Message));
    }

    // --- Map ---

    [Fact]
    public void Map_transforms_the_value_on_success()
    {
        Result<string> mapped = Result.Success(21).Map(value => $"n={value}");

        Assert.True(mapped.IsSuccess);
        Assert.Equal("n=21", mapped.Value);
    }

    [Fact]
    public void Map_propagates_the_errors_on_failure_and_does_not_invoke_the_map()
    {
        bool invoked = false;
        Result<string> mapped = Result.Failure<int>(SomeError).Map(value =>
        {
            invoked = true;
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });

        Assert.False(invoked);
        Assert.True(mapped.IsFailure);
        Assert.Equal(SomeError, mapped.Error);
    }

    [Fact]
    public void Map_rejects_a_map_that_returns_null()
    {
        Result<int> result = Result.Success(1);

        Assert.Throws<ArgumentNullException>(() => result.Map<string>(_ => null!));
    }

    // --- Bind ---

    [Fact]
    public void Bind_chains_a_result_returning_operation_on_success()
    {
        Result<int> bound = Result.Success(4).Bind(value => Result.Success(value + 1));

        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public void Bind_can_turn_a_success_into_a_failure()
    {
        Result<int> bound = Result.Success(4).Bind(_ => Result.Failure<int>(SomeError));

        Assert.True(bound.IsFailure);
        Assert.Equal(SomeError, bound.Error);
    }

    [Fact]
    public void Bind_propagates_the_errors_on_failure_and_does_not_invoke_the_continuation()
    {
        bool invoked = false;
        Result<int> bound = Result.Failure<int>(SomeError).Bind(value =>
        {
            invoked = true;
            return Result.Success(value);
        });

        Assert.False(invoked);
        Assert.Equal(SomeError, bound.Error);
    }

    // --- safe (non-throwing) accessors ---

    [Fact]
    public void GetValueOrDefault_returns_the_value_or_default()
    {
        Assert.Equal(7, Result.Success(7).GetValueOrDefault());
        Assert.Equal(0, Result.Failure<int>(SomeError).GetValueOrDefault());      // default(int)
        Assert.Null(Result.Failure<string>(SomeError).GetValueOrDefault());        // default(string)
    }

    [Fact]
    public void GetValueOrDefault_returns_the_fallback_on_failure()
    {
        Assert.Equal(7, Result.Success(7).GetValueOrDefault(99));
        Assert.Equal(99, Result.Failure<int>(SomeError).GetValueOrDefault(99));
    }

    [Fact]
    public void TryGetValue_reports_success_and_yields_the_value()
    {
        Assert.True(Result.Success(7).TryGetValue(out int value));
        Assert.Equal(7, value);

        Assert.False(Result.Failure<int>(SomeError).TryGetValue(out int fallback));
        Assert.Equal(0, fallback);
    }

    [Fact]
    public void The_safe_accessors_never_throw_on_a_failure()
    {
        Result<int> result = Result.Failure<int>(SomeError);

        // Exception-free access, unlike the throwing Value getter.
        _ = result.GetValueOrDefault();
        _ = result.GetValueOrDefault(1);
        _ = result.TryGetValue(out _);
    }

    // --- guards ---

    [Fact]
    public void The_combinators_reject_null_delegates()
    {
        Result<int> result = Result.Success(1);

        Assert.Throws<ArgumentNullException>(() => result.Match<int>(null!, _ => 0));
        Assert.Throws<ArgumentNullException>(() => result.Match<int>(value => value, null!));
        Assert.Throws<ArgumentNullException>(() => result.Map<int>(null!));
        Assert.Throws<ArgumentNullException>(() => result.Bind<int>(null!));
    }

    // --- non-generic Result.Match ---

    [Fact]
    public void NonGeneric_Match_folds_success_and_failure()
    {
        Assert.Equal("ok", Result.Success().Match(() => "ok", _ => "fail"));
        Assert.Equal("fail:boom", Result.Failure(SomeError).Match(() => "ok", errors => $"fail:{errors[0].Message}"));
    }

    [Fact]
    public void NonGeneric_Match_rejects_null_delegates()
    {
        Result result = Result.Success();

        Assert.Throws<ArgumentNullException>(() => result.Match<int>(null!, _ => 0));
        Assert.Throws<ArgumentNullException>(() => result.Match<int>(() => 0, null!));
    }
}
