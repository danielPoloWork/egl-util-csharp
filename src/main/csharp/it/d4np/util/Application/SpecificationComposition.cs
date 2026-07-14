using System.Linq.Expressions;

namespace It.D4np.Util.Application;

/// <summary>
/// Combines two <c>Expression&lt;Func&lt;TEntity, bool&gt;&gt;</c> predicates over a <b>single</b> parameter,
/// rebinding the right-hand expression's parameter onto the left's. Composing via <c>Expression.Invoke</c>
/// would leave a nested invocation EF Core cannot translate; rebinding produces a flat tree that translates
/// to SQL. Backs the <see cref="ISpecification{TEntity}"/> And/Or/Not combinators (#68).
/// </summary>
internal static class SpecificationCriteria
{
    public static Expression<Func<TEntity, bool>> AndAlso<TEntity>(
        Expression<Func<TEntity, bool>> left, Expression<Func<TEntity, bool>> right) =>
        Combine(left, right, Expression.AndAlso);

    public static Expression<Func<TEntity, bool>> OrElse<TEntity>(
        Expression<Func<TEntity, bool>> left, Expression<Func<TEntity, bool>> right) =>
        Combine(left, right, Expression.OrElse);

    public static Expression<Func<TEntity, bool>> Not<TEntity>(Expression<Func<TEntity, bool>> operand) =>
        Expression.Lambda<Func<TEntity, bool>>(Expression.Not(operand.Body), operand.Parameters[0]);

    private static Expression<Func<TEntity, bool>> Combine<TEntity>(
        Expression<Func<TEntity, bool>> left,
        Expression<Func<TEntity, bool>> right,
        Func<Expression, Expression, Expression> merge)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), left.Parameters[0].Name);
        Expression leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body);
        Expression rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<TEntity, bool>>(merge(leftBody, rightBody), parameter);
    }

    // Rewrites every occurrence of one parameter to another, so two lambdas can share a single parameter.
    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}

/// <summary>
/// The <see cref="ISpecification{TEntity}"/> produced by the And/Or/Not combinators: a pure filter
/// predicate with no includes, ordering, or paging. A <see langword="null"/> <see cref="Specification{TEntity}.Criteria"/>
/// means "match all".
/// </summary>
internal sealed class CriteriaSpecification<TEntity> : Specification<TEntity>
{
    public CriteriaSpecification()
    {
    }

    public CriteriaSpecification(Expression<Func<TEntity, bool>> criteria)
        : base(criteria)
    {
    }
}
