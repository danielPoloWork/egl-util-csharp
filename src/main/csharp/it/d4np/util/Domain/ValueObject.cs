namespace It.D4np.Util.Domain;

/// <summary>
/// Base class for value objects — immutable types defined entirely by their attribute values,
/// with no conceptual identity. Equality is structural: two value objects are equal if and only
/// if they are the same concrete type and the components returned by
/// <see cref="GetEqualityComponents"/> are pairwise equal, in order.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Returns the components that define this value object's value, in a stable order. Both
    /// equality and hashing are derived from this sequence; a component may be <see langword="null"/>.
    /// </summary>
    /// <returns>The ordered equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// Determines whether this value object is structurally equal to <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The value object to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the value objects are structurally equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(ValueObject? other) =>
        other is not null
        && GetType() == other.GetType()
        && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (object? component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>Determines whether two value objects are structurally equal.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two value objects are structurally unequal.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
