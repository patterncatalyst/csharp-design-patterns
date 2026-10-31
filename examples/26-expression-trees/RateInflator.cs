using System.Linq.Expressions;

namespace TariffExpressions;

/// <summary>
/// Rewrites a rate rule by scaling every <see cref="decimal"/> constant in
/// its tree by a factor, leaving every other constant untouched, and hands
/// back a brand-new <see cref="Expression{TDelegate}"/> rather than mutating
/// the one it was given. Expression trees are immutable, so a rewrite is
/// always a new tree built from the pieces the visitor returns.
/// </summary>
public sealed class RateInflator : ExpressionVisitor
{
    private decimal _factor;

    public Expression<Func<RateRequest, decimal>> Inflate(Expression<Func<RateRequest, decimal>> rule, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _factor = factor;
        return (Expression<Func<RateRequest, decimal>>)Visit(rule)!;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Value is decimal amount
            ? Expression.Constant(amount * _factor, typeof(decimal))
            : base.VisitConstant(node);
    }
}
