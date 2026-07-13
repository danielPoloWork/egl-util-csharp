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

    [Fact]
    public void Failure_rejects_Error_None()
    {
        // Regression for #29: Error.None is the sentinel carried by successful results. A failure
        // carrying it would satisfy the documented `result.Error == Error.None` success check and be
        // silently swallowed, so a failure must always carry a distinguishable error.
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
        Assert.Throws<ArgumentException>(() => Result.Failure(new Error("E1", "real"), Error.None));
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void Failure_rejects_a_null_error_element()
    {
        // #53: the array is guarded, but a null element would leak through the non-nullable Result.Error
        // and defer a NullReferenceException. It is now rejected up front with an ArgumentException.
        Assert.Throws<ArgumentException>(() => Result.Failure((Error)null!));
        Assert.Throws<ArgumentException>(() => Result.Failure(new Error("E1", "real"), null!));
        Assert.Throws<ArgumentException>(() => Result.Failure<int>((Error)null!));
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(new Error("E1", "real"), null!));
    }

    [Fact]
    public void Errors_is_a_read_only_view_that_cannot_be_mutated_through_a_downcast()
    {
        // #85: Errors must not be castable back to its mutable backing array — otherwise a consumer can
        // mutate a supposedly immutable Result after construction.
        Result result = Result.Failure(new Error("E1", "first"), new Error("E2", "second"));

        Assert.IsNotType<Error[]>(result.Errors);
        Assert.Throws<InvalidCastException>(() => _ = (Error[])result.Errors);
    }

    [Fact]
    public void A_subclass_supplied_mutable_list_cannot_mutate_errors_after_construction()
    {
        // #85: the protected constructor takes IReadOnlyList<Error>; a subclass could pass a mutable list
        // and keep mutating it. Errors must be an independent, read-only copy.
        var backing = new List<Error> { new("E1", "first") };
        var result = new SubclassResult(backing);

        backing[0] = new Error("E2", "changed"); // mutate the caller's list after construction

        Assert.Equal("E1", result.Errors[0].Code);
    }

    // A minimal Result subclass exercising the protected constructor's defensive copy (#85).
    private sealed class SubclassResult(IReadOnlyList<Error> errors) : Result(false, errors);
}
