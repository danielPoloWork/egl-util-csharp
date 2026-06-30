namespace It.D4np.Util.Domain;

/// <summary>
/// An immutable, value-equal description of a failure: a stable machine-readable <see cref="Code"/>
/// and a human-readable <see cref="Message"/>. <see cref="None"/> is the Null Object representing
/// the absence of an error (used by successful results).
/// </summary>
/// <param name="Code">A stable, machine-readable error code (e.g. <c>"User.Email.Required"</c>).</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>The canonical "no error" sentinel (Null Object), carried by successful results.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
