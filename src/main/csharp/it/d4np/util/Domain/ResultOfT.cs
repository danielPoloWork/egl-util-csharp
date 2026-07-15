using System.Diagnostics.CodeAnalysis;

namespace It.D4np.Util.Domain;

/// <summary>
/// The outcome of an operation that yields a value on success: a <see cref="Result"/> that also
/// carries a <typeparamref name="TValue"/> when <see cref="Result.IsSuccess"/> is <see langword="true"/>.
/// Reading <see cref="Value"/> on a failed result throws.
/// </summary>
/// <typeparam name="TValue">The success value type.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue _value;

    internal Result(TValue? value, bool isSuccess, IReadOnlyList<Error> errors)
        : base(isSuccess, errors)
    {
        _value = value!;
    }

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> when the result is a failure —
    /// guard with <see cref="Result.IsSuccess"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value =>
        IsSuccess ? _value : throw new InvalidOperationException("Cannot access the value of a failed result.");

    // --- Consumption / composition (#69). None of these throw for an expected failure; they compose the
    // functional Result model rather than forcing imperative IsSuccess/Value branching. ---

    /// <summary>
    /// Folds the result to a single value: <paramref name="onSuccess"/> applied to the success value, or
    /// <paramref name="onFailure"/> applied to the errors. Neither branch throws for an expected failure.
    /// </summary>
    /// <typeparam name="TResult">The folded result type.</typeparam>
    /// <param name="onSuccess">Maps the success value; invoked only when successful.</param>
    /// <param name="onFailure">Maps the errors; invoked only when failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onSuccess"/> or <paramref name="onFailure"/> is <see langword="null"/>.</exception>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<IReadOnlyList<Error>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value) : onFailure(Errors);
    }

    /// <summary>
    /// Transforms the success value with <paramref name="map"/> (a functor map), propagating the errors
    /// unchanged on failure. <paramref name="map"/> runs only on success and must not return <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TNew">The mapped value type.</typeparam>
    /// <param name="map">The value transform; invoked only when successful.</param>
    /// <returns>A success carrying <c>map(value)</c>, or the same failure typed as <typeparamref name="TNew"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>, or it returns <see langword="null"/>.</exception>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Success(map(_value)) : Failure<TNew>([.. Errors]);
    }

    /// <summary>
    /// Chains a result-returning operation onto the success value (a monadic bind), propagating the errors
    /// unchanged on failure. <paramref name="bind"/> runs only on success.
    /// </summary>
    /// <typeparam name="TNew">The chained value type.</typeparam>
    /// <param name="bind">The continuation; invoked only when successful.</param>
    /// <returns>The result of <paramref name="bind"/>, or the same failure typed as <typeparamref name="TNew"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bind"/> is <see langword="null"/>.</exception>
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return IsSuccess ? bind(_value) : Failure<TNew>([.. Errors]);
    }

    /// <summary>Returns the success value, or <see langword="default"/> when the result is a failure (never throws).</summary>
    /// <returns>The value on success; otherwise <see langword="default"/>.</returns>
    public TValue? GetValueOrDefault() => IsSuccess ? _value : default;

    /// <summary>Returns the success value, or <paramref name="fallback"/> when the result is a failure (never throws).</summary>
    /// <param name="fallback">The value to return on failure (returned verbatim).</param>
    /// <returns>The value on success; otherwise <paramref name="fallback"/>.</returns>
    public TValue GetValueOrDefault(TValue fallback) => IsSuccess ? _value : fallback;

    /// <summary>
    /// Non-throwing value access: on success sets <paramref name="value"/> to the value and returns
    /// <see langword="true"/>; on failure sets it to <see langword="default"/> and returns <see langword="false"/>.
    /// </summary>
    /// <param name="value">Receives the success value, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> if the result is a success.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }

    /// <summary>
    /// Implicitly wraps a value as a successful result. Failures are created explicitly with
    /// <see cref="Result.Failure{TValue}(Error[])"/> — there is deliberately no implicit
    /// <see cref="Error"/> → <see cref="Result{TValue}"/> conversion (it collided with this operator and
    /// silently turned a <c>Result&lt;object&gt;</c> carrying an error into a failure; ADR-0020).
    /// </summary>
    /// <param name="value">The success value; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
