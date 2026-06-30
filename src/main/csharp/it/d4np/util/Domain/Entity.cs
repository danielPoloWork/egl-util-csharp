namespace It.D4np.Util.Domain;

/// <summary>
/// Base class for domain entities — objects defined by a continuous identity rather than by
/// their attribute values. Two entities are equal if and only if they are the same concrete
/// type and share the same <see cref="Id"/>.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type; must be non-nullable.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>The entity's strongly-typed identity.</summary>
    public TId Id { get; }

    /// <summary>Initializes a new entity with the given identity.</summary>
    /// <param name="id">The entity identity; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    /// <summary>
    /// Determines whether this entity has the same identity as <paramref name="other"/> — that
    /// is, the same concrete type and an equal <see cref="Id"/>.
    /// </summary>
    /// <param name="other">The entity to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the entities share an identity; otherwise <see langword="false"/>.</returns>
    public bool Equals(Entity<TId>? other) =>
        other is not null
        && GetType() == other.GetType()
        && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Determines whether two entities share the same identity.</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Determines whether two entities have different identities.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
