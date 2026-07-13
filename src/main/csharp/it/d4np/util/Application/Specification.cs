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
    private readonly List<(Expression<Func<TEntity, object>> KeySelector, bool Descending)> _thenBys = [];

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
    public IReadOnlyList<(Expression<Func<TEntity, object>> KeySelector, bool Descending)> ThenBys => _thenBys.AsReadOnly();

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

    /// <summary>
    /// Orders the result ascending by the given key, replacing any prior primary ordering and clearing
    /// any secondary <c>ThenBy</c> keys (which belonged to the previous primary ordering).
    /// </summary>
    /// <param name="orderBy">The ascending sort key; must not be <see langword="null"/>.</param>
    protected void ApplyOrderBy(Expression<Func<TEntity, object>> orderBy)
    {
        ArgumentNullException.ThrowIfNull(orderBy);
        OrderBy = orderBy;
        OrderByDescending = null;
        _thenBys.Clear();
    }

    /// <summary>
    /// Orders the result descending by the given key, replacing any prior primary ordering and clearing
    /// any secondary <c>ThenBy</c> keys (which belonged to the previous primary ordering).
    /// </summary>
    /// <param name="orderByDescending">The descending sort key; must not be <see langword="null"/>.</param>
    protected void ApplyOrderByDescending(Expression<Func<TEntity, object>> orderByDescending)
    {
        ArgumentNullException.ThrowIfNull(orderByDescending);
        OrderByDescending = orderByDescending;
        OrderBy = null;
        _thenBys.Clear();
    }

    /// <summary>
    /// Adds a secondary ascending sort key, applied after the primary ordering as <c>ThenBy</c>. Multiple
    /// calls chain in order. Use this to give paging a deterministic tie-breaker over a non-unique primary
    /// key.
    /// </summary>
    /// <param name="thenBy">The secondary ascending sort key; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="thenBy"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No primary ordering has been applied yet.</exception>
    protected void ApplyThenBy(Expression<Func<TEntity, object>> thenBy)
    {
        ArgumentNullException.ThrowIfNull(thenBy);
        RequirePrimaryOrdering();
        _thenBys.Add((thenBy, Descending: false));
    }

    /// <summary>
    /// Adds a secondary descending sort key, applied after the primary ordering as <c>ThenByDescending</c>.
    /// Multiple calls chain in order. Use this to give paging a deterministic tie-breaker over a non-unique
    /// primary key.
    /// </summary>
    /// <param name="thenByDescending">The secondary descending sort key; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="thenByDescending"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No primary ordering has been applied yet.</exception>
    protected void ApplyThenByDescending(Expression<Func<TEntity, object>> thenByDescending)
    {
        ArgumentNullException.ThrowIfNull(thenByDescending);
        RequirePrimaryOrdering();
        _thenBys.Add((thenByDescending, Descending: true));
    }

    private void RequirePrimaryOrdering()
    {
        if (OrderBy is null && OrderByDescending is null)
        {
            throw new InvalidOperationException(
                "Apply a primary ordering with ApplyOrderBy or ApplyOrderByDescending before adding a " +
                "secondary key with ApplyThenBy or ApplyThenByDescending.");
        }
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
