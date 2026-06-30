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

    /// <summary>The number of elements to skip when <see cref="IsPagingEnabled"/> is <see langword="true"/>.</summary>
    int Skip { get; }

    /// <summary>The number of elements to take when <see cref="IsPagingEnabled"/> is <see langword="true"/>.</summary>
    int Take { get; }

    /// <summary>Whether <see cref="Skip"/>/<see cref="Take"/> paging should be applied.</summary>
    bool IsPagingEnabled { get; }
}
