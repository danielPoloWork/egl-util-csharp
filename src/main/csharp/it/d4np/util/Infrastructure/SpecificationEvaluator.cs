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
    /// <returns>
    /// The composed queryable. <b>Note:</b> when the specification enables paging the query is already
    /// <c>Skip</c>/<c>Take</c>-sliced, so counting it yields at most the page size, not the size of the
    /// matching set — count with the criteria-only overload
    /// (<see cref="GetQuery{TEntity}(IQueryable{TEntity}, ISpecification{TEntity}, bool)"/>) or page with
    /// <see cref="ToPaginatedListAsync{TEntity}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="specification"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="specification"/> is in an invalid ordering state: it defines <em>both</em>
    /// <c>OrderBy</c> and <c>OrderByDescending</c> (the primary ordering is a single ascending <em>or</em>
    /// descending key — see <see cref="ISpecification{TEntity}.OrderBy"/>), or it enables paging
    /// (<c>IsPagingEnabled</c>) with no primary ordering at all. Both are rejected rather than run: the first
    /// would silently drop one ordering, the second returns rows in an undefined order across pages on
    /// relational providers.
    /// </exception>
    public static IQueryable<TEntity> GetQuery<TEntity>(IQueryable<TEntity> source, ISpecification<TEntity> specification)
        where TEntity : class => GetQuery(source, specification, evaluateCriteriaOnly: false);

    /// <summary>
    /// Builds the query described by <paramref name="specification"/> over <paramref name="source"/>. When
    /// <paramref name="evaluateCriteriaOnly"/> is <see langword="true"/> only the filter criteria is applied
    /// — includes, ordering, and paging are skipped — so the query counts the full matching set (e.g. a paged
    /// total) rather than a single page (#70).
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="source">The base queryable (typically a <c>DbSet&lt;TEntity&gt;</c>).</param>
    /// <param name="specification">The specification to apply.</param>
    /// <param name="evaluateCriteriaOnly">Apply only the filter criteria, skipping includes, ordering, and paging.</param>
    /// <returns>The composed queryable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="specification"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// (Full evaluation only.) <paramref name="specification"/> defines both <c>OrderBy</c> and
    /// <c>OrderByDescending</c>, or enables paging with no primary ordering (see the two-argument overload).
    /// </exception>
    public static IQueryable<TEntity> GetQuery<TEntity>(
        IQueryable<TEntity> source, ISpecification<TEntity> specification, bool evaluateCriteriaOnly)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);

        IQueryable<TEntity> query = source;

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        // Criteria-only: skip includes/ordering/paging so the caller can count the full matching set — e.g.
        // the total for a page — instead of the (Skip/Take-sliced) page itself (#70).
        if (evaluateCriteriaOnly)
        {
            return query;
        }

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        // The primary ordering is a single ascending OR descending key; the two are mutually exclusive. The
        // Specification base class enforces this (ApplyOrderBy/ApplyOrderByDescending reset each other), but
        // a direct ISpecification implementation could set both. Rather than silently apply OrderBy and drop
        // OrderByDescending — yielding wrongly-ordered and, when paged, wrongly-sliced results — reject the
        // ambiguous state, mirroring the paged-but-unordered guard below (#88).
        if (specification.OrderBy is not null && specification.OrderByDescending is not null)
        {
            throw new InvalidOperationException(
                "A specification must not define both OrderBy and OrderByDescending; the primary ordering is " +
                "a single ascending or descending key. Set exactly one (use ThenBy/ThenByDescending for " +
                "secondary keys).");
        }

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

    /// <summary>
    /// Runs <paramref name="specification"/> as a single page: counts the full matching set (criteria only,
    /// so the total is the match count, not the page size) and materializes the requested page, returning a
    /// <see cref="PaginatedList{TEntity}"/> (#70). This method owns the paging — <paramref name="pageIndex"/>
    /// is 1-based — so the specification supplies the filter and a <b>required ordering</b> but must
    /// <b>not</b> enable its own paging.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="source">The base queryable (typically a <c>DbSet&lt;TEntity&gt;</c>).</param>
    /// <param name="specification">The filter + ordering; must define an ordering and must not enable paging.</param>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the count and page queries.</param>
    /// <returns>The requested page and the total matching count, as a <see cref="PaginatedList{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="specification"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> or <paramref name="pageSize"/> is not positive.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="specification"/> enables its own paging, or defines no primary ordering (paging an
    /// unordered query is non-deterministic across pages).
    /// </exception>
    public static async Task<PaginatedList<TEntity>> ToPaginatedListAsync<TEntity>(
        IQueryable<TEntity> source,
        ISpecification<TEntity> specification,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        if (specification.IsPagingEnabled)
        {
            throw new InvalidOperationException(
                "ToPaginatedListAsync applies the page itself; pass a specification that defines the filter " +
                "and ordering but does not enable paging (no ApplyPaging).");
        }

        if (specification.OrderBy is null && specification.OrderByDescending is null)
        {
            throw new InvalidOperationException(
                "A paged query must define an ordering (OrderBy or OrderByDescending); paging an unordered " +
                "query is non-deterministic across pages.");
        }

        int totalCount = await GetQuery(source, specification, evaluateCriteriaOnly: true)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TEntity> items = await GetQuery(source, specification)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<TEntity>(items, pageIndex, pageSize, totalCount);
    }
}
