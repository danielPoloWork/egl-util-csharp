namespace It.D4np.Util.Domain;

/// <summary>
/// An immutable, value-equal description of a failure: a stable machine-readable <see cref="Code"/>
/// and a human-readable <see cref="Message"/>. <see cref="None"/> is the Null Object representing
/// the absence of an error (used by successful results).
/// </summary>
/// <param name="Code">A stable, machine-readable error code (e.g. <c>"User.Email.Required"</c>).</param>
/// <param name="Message">A human-readable description of the failure.</param>
/// <exception cref="ArgumentNullException">
/// <paramref name="Code"/> or <paramref name="Message"/> is <see langword="null"/>.
/// </exception>
public sealed record Error(string Code, string Message)
{
    /// <summary>The canonical "no error" sentinel (Null Object), carried by successful results.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    // Back the non-nullable annotations with runtime guards. Re-declaring the positional properties with a
    // validating initializer keeps the record fully positional (constructor, Deconstruct, `with`, equality)
    // yet rejects null on every construction path the annotations don't police — a `#nullable disable`
    // caller, an explicit `null!`, or System.Text.Json materializing an Error from a payload with a missing
    // Code/Message via the parameterized constructor (#76). The empty-string sentinel (None) stays valid.

    /// <summary>A stable, machine-readable error code (e.g. <c>"User.Email.Required"</c>); never <see langword="null"/>.</summary>
    public string Code { get; init; } = Code ?? throw new ArgumentNullException(nameof(Code));

    /// <summary>A human-readable description of the failure; never <see langword="null"/>.</summary>
    public string Message { get; init; } = Message ?? throw new ArgumentNullException(nameof(Message));
}
