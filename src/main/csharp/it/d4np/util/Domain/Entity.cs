using System.Runtime.CompilerServices;

namespace It.D4np.Util.Domain;

/// <summary>
/// Base class for domain entities — objects defined by a continuous identity rather than by their
/// attribute values. The self-referencing <typeparamref name="TEntity"/> is the entity's <b>canonical
/// domain type</b>: two entities are equal if and only if they are the same canonical type and share a
/// non-default <see cref="Id"/>.
/// </summary>
/// <remarks>
/// <para>
/// Equality is based on the canonical type <typeparamref name="TEntity"/>, not the runtime type, so an
/// entity materialized as a runtime subclass — an EF Core lazy-loading/change-tracking <b>proxy</b>
/// (<c>CustomerProxy : Customer</c>), or any subclass — compares equal to a plainly-constructed instance
/// of the same identity, with no reference to EF and no proxy-name heuristic (#26). A concrete entity
/// closes the generic over itself: <c>class Customer : Entity&lt;Guid, Customer&gt;</c>.
/// </para>
/// <para>
/// <b>Transient identity.</b> An entity whose <see cref="Id"/> is the type default (<c>0</c>,
/// <see cref="System.Guid.Empty"/>) is <i>transient</i> — not yet assigned a persistent identity — and
/// compares by <b>reference</b>, so two distinct unsaved entities are unequal and do not collapse into a
/// single slot in a <c>HashSet</c>/<c>Dictionary</c>. Once assigned a non-default identity, an entity
/// compares by that identity (#25). A reference-typed <typeparamref name="TId"/> is never transient (the
/// constructor rejects a <see langword="null"/> identity).
/// </para>
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type; must be non-nullable.</typeparam>
/// <typeparam name="TEntity">The canonical entity type (the concrete type closes the generic over itself).</typeparam>
public abstract class Entity<TId, TEntity> : IEquatable<TEntity>
    where TId : notnull
    where TEntity : Entity<TId, TEntity>
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
    /// Determines whether this entity has the same identity as <paramref name="other"/> — the same
    /// canonical type <typeparamref name="TEntity"/> and an equal, non-default <see cref="Id"/>. A
    /// transient entity (default <see cref="Id"/>) is equal only to itself.
    /// </summary>
    /// <param name="other">The entity to compare with, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the entities share an identity; otherwise <see langword="false"/>.</returns>
    public bool Equals(TEntity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Transient entities (default/unset Id) have no persistent identity yet, so they are equal only
        // by reference — never merged by a shared default Id (#25).
        if (IsTransient(Id) || IsTransient(other.Id))
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TEntity other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        // A transient entity hashes by reference (identity), matching its reference equality; an assigned
        // entity hashes on its canonical type and Id, so a proxy and a plain instance of the same identity
        // share a hash.
        IsTransient(Id) ? RuntimeHelpers.GetHashCode(this) : HashCode.Combine(typeof(TEntity), Id);

    /// <summary>Determines whether two entities share the same identity.</summary>
    public static bool operator ==(Entity<TId, TEntity>? left, Entity<TId, TEntity>? right) =>
        left is null ? right is null : left.Equals(right as TEntity);

    /// <summary>Determines whether two entities have different identities.</summary>
    public static bool operator !=(Entity<TId, TEntity>? left, Entity<TId, TEntity>? right) => !(left == right);

    private static bool IsTransient(TId id) => EqualityComparer<TId>.Default.Equals(id, default!);
}
