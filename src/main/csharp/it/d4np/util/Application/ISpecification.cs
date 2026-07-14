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

    /// <summary>
    /// The ascending sort key, or <see langword="null"/> if not sorted ascending. The primary ordering is a
    /// single key: <see cref="OrderBy"/> and <see cref="OrderByDescending"/> are <b>mutually exclusive</b> —
    /// at most one may be non-<see langword="null"/>. The evaluator rejects a specification that sets both
    /// with an <see cref="System.InvalidOperationException"/>. (The <c>Specification</c> base class enforces
    /// this automatically; a direct implementation must uphold it.) Use <see cref="ThenBys"/> for secondary
    /// keys.
    /// </summary>
    Expression<Func<TEntity, object>>? OrderBy { get; }

    /// <summary>
    /// The descending sort key, or <see langword="null"/> if not sorted descending. Mutually exclusive with
    /// <see cref="OrderBy"/> — see there.
    /// </summary>
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

    // --- Predicate composition (#68). These default members combine only the filter Criteria; the result
    // carries no includes, ordering, or paging. A null Criteria means "match all". Predicates are merged
    // over a single parameter (not Expression.Invoke), so the composed Criteria stays EF Core-translatable. ---

    /// <summary>
    /// Returns a specification whose <see cref="Criteria"/> matches entities satisfying <b>both</b> this
    /// specification's criteria and <paramref name="other"/>'s (logical AND). A <see langword="null"/>
    /// <see cref="Criteria"/> is the identity ("match all"), so <c>x.And(y)</c> with one side unfiltered
    /// yields the other side's criteria.
    /// </summary>
    /// <param name="other">The specification to AND with; must not be <see langword="null"/>.</param>
    /// <returns>A new predicate-only specification for the combined criteria.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    ISpecification<TEntity> And(ISpecification<TEntity> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Criteria is null)
        {
            return other.Criteria is null
                ? new CriteriaSpecification<TEntity>()
                : new CriteriaSpecification<TEntity>(other.Criteria);
        }

        return other.Criteria is null
            ? new CriteriaSpecification<TEntity>(Criteria)
            : new CriteriaSpecification<TEntity>(SpecificationCriteria.AndAlso(Criteria, other.Criteria));
    }

    /// <summary>
    /// Returns a specification whose <see cref="Criteria"/> matches entities satisfying <b>either</b> this
    /// specification's criteria or <paramref name="other"/>'s (logical OR). Because a <see langword="null"/>
    /// <see cref="Criteria"/> means "match all", OR-ing with an unfiltered specification yields an unfiltered
    /// (match-all) result.
    /// </summary>
    /// <param name="other">The specification to OR with; must not be <see langword="null"/>.</param>
    /// <returns>A new predicate-only specification for the combined criteria.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    ISpecification<TEntity> Or(ISpecification<TEntity> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Criteria is null || other.Criteria is null
            ? new CriteriaSpecification<TEntity>()
            : new CriteriaSpecification<TEntity>(SpecificationCriteria.OrElse(Criteria, other.Criteria));
    }

    /// <summary>
    /// Returns a specification whose <see cref="Criteria"/> matches entities <b>not</b> satisfying this
    /// specification's criteria (logical NOT). Negating a <see langword="null"/> <see cref="Criteria"/>
    /// ("match all") yields a match-nothing specification.
    /// </summary>
    /// <returns>A new predicate-only specification for the negated criteria.</returns>
    ISpecification<TEntity> Not() =>
        Criteria is null
            ? new CriteriaSpecification<TEntity>(_ => false)
            : new CriteriaSpecification<TEntity>(SpecificationCriteria.Not(Criteria));

    /// <summary>
    /// Evaluates this specification's <see cref="Criteria"/> against <paramref name="entity"/> <b>in memory</b>
    /// (a <see langword="null"/> <see cref="Criteria"/> matches everything). Independent of the data layer,
    /// so it is useful for validation or unit tests. <b>Note:</b> the default implementation compiles the
    /// criteria on each call — for a hot loop, compile and cache the delegate (or override this).
    /// </summary>
    /// <param name="entity">The candidate entity; must not be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the entity satisfies the criteria (or there is none).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    bool IsSatisfiedBy(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Criteria is null || Criteria.Compile()(entity);
    }
}
