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

    /// <summary>Implicitly wraps a value as a successful result.</summary>
    /// <param name="value">The success value.</param>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Implicitly wraps an error as a failed result.</summary>
    /// <param name="error">The failure error; must not be <see cref="Error.None"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="error"/> is <see cref="Error.None"/>.</exception>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
