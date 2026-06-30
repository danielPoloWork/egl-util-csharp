using It.D4np.Util.Domain;

namespace It.D4np.Util.Application;

/// <summary>
/// A generic abstraction for persisting and querying an aggregate root. Implementations live in
/// the infrastructure layer; the domain and application depend only on this contract.
/// </summary>
/// <typeparam name="TEntity">The aggregate-root type.</typeparam>
/// <typeparam name="TId">The aggregate's strongly-typed identifier.</typeparam>
public interface IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Finds an aggregate by its identity.</summary>
    /// <param name="id">The aggregate identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The aggregate, or <see langword="null"/> if none exists.</returns>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Lists the aggregates matching a specification.</summary>
    /// <param name="specification">The query specification.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching aggregates.</returns>
    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>Stages a new aggregate for insertion.</summary>
    /// <param name="entity">The aggregate to add.</param>
    void Add(TEntity entity);

    /// <summary>Stages an existing aggregate for update.</summary>
    /// <param name="entity">The aggregate to update.</param>
    void Update(TEntity entity);

    /// <summary>Stages an aggregate for removal.</summary>
    /// <param name="entity">The aggregate to remove.</param>
    void Remove(TEntity entity);
}
