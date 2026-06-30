using It.D4np.Util.Application;

using Microsoft.EntityFrameworkCore;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Applies an <see cref="ISpecification{TEntity}"/> to an <see cref="IQueryable{TEntity}"/>,
/// composing its criteria, includes, ordering, and paging into a single EF Core query.
/// </summary>
public static class SpecificationEvaluator
{
    /// <summary>Builds the query described by <paramref name="specification"/> over <paramref name="source"/>.</summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="source">The base queryable (typically a <c>DbSet&lt;TEntity&gt;</c>).</param>
    /// <param name="specification">The specification to apply.</param>
    /// <returns>The composed queryable, ready to enumerate or count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="specification"/> is <see langword="null"/>.</exception>
    public static IQueryable<TEntity> GetQuery<TEntity>(IQueryable<TEntity> source, ISpecification<TEntity> specification)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        IQueryable<TEntity> query = source;

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending is not null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        if (specification.IsPagingEnabled)
        {
            if (specification.OrderBy is null && specification.OrderByDescending is null)
            {
                throw new InvalidOperationException(
                    "A paged specification must define an ordering (OrderBy or OrderByDescending); " +
                    "paging an unordered query is non-deterministic on relational providers.");
            }

            query = query.Skip(specification.Skip).Take(specification.Take);
        }

        return query;
    }
}
