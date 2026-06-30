namespace It.D4np.Util.Domain;

/// <summary>
/// Represents the outcome of an operation without throwing: either success, or failure carrying
/// one or more <see cref="Error"/>s. Construct instances with <see cref="Success()"/> /
/// <see cref="Failure(Error[])"/> and their generic overloads, and aggregate with
/// <see cref="Combine(Result[])"/>.
/// </summary>
public class Result
{
    private static readonly IReadOnlyList<Error> NoErrors = [];

    /// <summary>
    /// Initializes a result, enforcing the core invariant: a success carries no errors and a
    /// failure carries at least one.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="errors">The failure errors; empty when <paramref name="isSuccess"/> is <see langword="true"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The success/errors invariant is violated.</exception>
    protected Result(bool isSuccess, IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (isSuccess && errors.Count > 0)
        {
            throw new ArgumentException("A successful result cannot carry errors.", nameof(errors));
        }

        if (!isSuccess && errors.Count == 0)
        {
            throw new ArgumentException("A failed result must carry at least one error.", nameof(errors));
        }

        IsSuccess = isSuccess;
        Errors = errors;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed (the negation of <see cref="IsSuccess"/>).</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The errors describing the failure, in order; empty when <see cref="IsSuccess"/>.</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>The first failure error, or <see cref="Error.None"/> when the result is successful.</summary>
    public Error Error => Errors.Count > 0 ? Errors[0] : Error.None;

    /// <summary>Creates a successful result.</summary>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success() => new(true, NoErrors);

    /// <summary>Creates a failed result carrying the given errors.</summary>
    /// <param name="errors">One or more errors; must be non-null and non-empty.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(params Error[] errors) => new(false, Defensive(errors));

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <typeparam name="TValue">The success value type.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, NoErrors);

    /// <summary>Creates a failed result of <typeparamref name="TValue"/> carrying the given errors.</summary>
    /// <typeparam name="TValue">The success value type.</typeparam>
    /// <param name="errors">One or more errors; must be non-null and non-empty.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Failure<TValue>(params Error[] errors) => new(default, false, Defensive(errors));

    /// <summary>
    /// Combines several results into one: successful if all succeed, otherwise a failure carrying
    /// every error from the failed results, in order. With no arguments, returns <see cref="Success()"/>
    /// (the identity element).
    /// </summary>
    /// <param name="results">The results to combine; must be non-null (individual results non-null).</param>
    /// <returns>The aggregated <see cref="Result"/>.</returns>
    public static Result Combine(params Result[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Error[] errors = results.SelectMany(result => result.Errors).ToArray();
        return errors.Length == 0 ? Success() : Failure(errors);
    }

    private static IReadOnlyList<Error> Defensive(Error[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.ToArray();
    }
}
