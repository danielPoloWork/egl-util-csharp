namespace It.D4np.Util.Application;

/// <summary>
/// The Unit of Work: commits all changes staged across repositories within a single atomic
/// transaction boundary.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all tracked changes as one transaction.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the store.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
