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
    /// <exception cref="InvalidOperationException">
    /// <paramref name="specification"/> enables paging (<c>IsPagingEnabled</c>) but defines no primary
    /// ordering (<c>OrderBy</c> / <c>OrderByDescending</c>): paging an unordered query returns rows in an
    /// undefined order across pages on relational providers, so it is rejected rather than run.
    /// </exception>
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

        IOrderedQueryable<TEntity>? ordered = specification.OrderBy is not null
            ? query.OrderBy(specification.OrderBy)
            : specification.OrderByDescending is not null
                ? query.OrderByDescending(specification.OrderByDescending)
                : null;

        if (ordered is not null)
        {
            // Apply secondary keys after the primary, in order, as ThenBy/ThenByDescending — a
            // deterministic tie-breaker for paging over a non-unique primary key (#31). Ignored when there
            // is no primary ordering (ThenBy has nothing to chain onto).
            foreach (var (keySelector, descending) in specification.ThenBys)
            {
                ordered = descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
            }

            query = ordered;
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
