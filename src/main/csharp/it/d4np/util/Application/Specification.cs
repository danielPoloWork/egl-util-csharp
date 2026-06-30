using System.Linq.Expressions;

namespace It.D4np.Util.Application;

/// <summary>
/// A convenient base class for building <see cref="ISpecification{TEntity}"/>s: derived
/// specifications set their filter via the constructor and compose includes, ordering, and paging
/// through the protected <c>AddInclude</c> / <c>ApplyOrderBy</c> / <c>ApplyPaging</c> helpers.
/// </summary>
/// <typeparam name="TEntity">The queried entity type.</typeparam>
public abstract class Specification<TEntity> : ISpecification<TEntity>
{
    private readonly List<Expression<Func<TEntity, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];

    /// <summary>Initializes a specification with no filter criteria.</summary>
    protected Specification()
    {
    }

    /// <summary>Initializes a specification with the given filter criteria.</summary>
    /// <param name="criteria">The filter predicate; must not be <see langword="null"/>.</param>
    protected Specification(Expression<Func<TEntity, bool>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        Criteria = criteria;
    }

    /// <inheritdoc />
    public Expression<Func<TEntity, bool>>? Criteria { get; }

    /// <inheritdoc />
    public IReadOnlyList<Expression<Func<TEntity, object>>> Includes => _includes.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<string> IncludeStrings => _includeStrings.AsReadOnly();

    /// <inheritdoc />
    public Expression<Func<TEntity, object>>? OrderBy { get; private set; }

    /// <inheritdoc />
    public Expression<Func<TEntity, object>>? OrderByDescending { get; private set; }

    /// <inheritdoc />
    public int Skip { get; private set; }

    /// <inheritdoc />
    public int Take { get; private set; }

    /// <inheritdoc />
    public bool IsPagingEnabled { get; private set; }

    /// <summary>Adds a related navigation to eager-load.</summary>
    /// <param name="include">The member-access expression to include; must not be <see langword="null"/>.</param>
    protected void AddInclude(Expression<Func<TEntity, object>> include)
    {
        ArgumentNullException.ThrowIfNull(include);
        _includes.Add(include);
    }

    /// <summary>Adds a dotted navigation path (e.g. <c>"Orders.Items"</c>) to eager-load, enabling multi-level loading.</summary>
    /// <param name="includeString">The navigation path; must not be null or whitespace.</param>
    protected void AddInclude(string includeString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(includeString);
        _includeStrings.Add(includeString);
    }

    /// <summary>Orders the result ascending by the given key (replacing any prior ordering).</summary>
    /// <param name="orderBy">The ascending sort key; must not be <see langword="null"/>.</param>
    protected void ApplyOrderBy(Expression<Func<TEntity, object>> orderBy)
    {
        ArgumentNullException.ThrowIfNull(orderBy);
        OrderBy = orderBy;
        OrderByDescending = null;
    }

    /// <summary>Orders the result descending by the given key (replacing any prior ordering).</summary>
    /// <param name="orderByDescending">The descending sort key; must not be <see langword="null"/>.</param>
    protected void ApplyOrderByDescending(Expression<Func<TEntity, object>> orderByDescending)
    {
        ArgumentNullException.ThrowIfNull(orderByDescending);
        OrderByDescending = orderByDescending;
        OrderBy = null;
    }

    /// <summary>Enables paging: skip <paramref name="skip"/> elements, then take <paramref name="take"/>.</summary>
    /// <param name="skip">The number of elements to skip; must be non-negative.</param>
    /// <param name="take">The number of elements to take; must be non-negative.</param>
    protected void ApplyPaging(int skip, int take)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegative(take);
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }
}
