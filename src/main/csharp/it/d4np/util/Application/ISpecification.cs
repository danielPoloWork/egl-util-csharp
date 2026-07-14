using System.Linq.Expressions;

namespace It.D4np.Util.Application;

/// <summary>
/// The Specification pattern: a declarative, composable description of a query over
/// <typeparamref name="TEntity"/> — its filter criteria, eager-load includes, ordering, and
/// paging — isolated from the data layer that evaluates it (see the infrastructure
/// <c>SpecificationEvaluator</c>).
/// </summary>
/// <typeparam name="TEntity">The queried entity type.</typeparam>
public interface ISpecification<TEntity>
{
    /// <summary>The filter predicate, or <see langword="null"/> for no filtering.</summary>
    Expression<Func<TEntity, bool>>? Criteria { get; }

    /// <summary>
    /// Single-level related data to eager-load, as member-access expressions. For multi-level
    /// (<c>ThenInclude</c>) chains, use <see cref="IncludeStrings"/>.
    /// </summary>
    IReadOnlyList<Expression<Func<TEntity, object>>> Includes { get; }

    /// <summary>
    /// Related data to eager-load as dotted navigation paths (e.g. <c>"Orders.Items"</c>), enabling
    /// the multi-level eager loading that <see cref="Includes"/> cannot express.
    /// </summary>
    IReadOnlyList<string> IncludeStrings { get; }

    /// <summary>The ascending sort key, or <see langword="null"/> if not sorted ascending.</summary>
    Expression<Func<TEntity, object>>? OrderBy { get; }

    /// <summary>The descending sort key, or <see langword="null"/> if not sorted descending.</summary>
    Expression<Func<TEntity, object>>? OrderByDescending { get; }

    /// <summary>
    /// Secondary sort keys applied after the primary <see cref="OrderBy"/>/<see cref="OrderByDescending"/>,
    /// in order, as <c>ThenBy</c>/<c>ThenByDescending</c> — each with its own direction (<c>Descending</c>).
    /// Empty when there is no secondary ordering, and ignored when there is no primary ordering. These give
    /// a deterministic tie-breaker for paging over a non-unique primary key (EF Core cannot express a
    /// multi-column sort through a single composite key expression). A <b>default</b> implementation returns
    /// an empty list, so this member is additive — existing implementors need not provide it.
    /// </summary>
    IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)> ThenBys => [];

    /// <summary>The number of elements to skip when <see cref="IsPagingEnabled"/> is <see langword="true"/>.</summary>
    int Skip { get; }

    /// <summary>The number of elements to take when <see cref="IsPagingEnabled"/> is <see langword="true"/>.</summary>
    int Take { get; }

    /// <summary>
    /// Whether <see cref="Skip"/>/<see cref="Take"/> paging should be applied. Paging requires a primary
    /// ordering (<see cref="OrderBy"/> or <see cref="OrderByDescending"/>): the evaluator rejects a
    /// paged-but-unordered specification with an <see cref="System.InvalidOperationException"/>, since
    /// paging an unordered query is non-deterministic across pages on relational providers.
    /// </summary>
    bool IsPagingEnabled { get; }
}
