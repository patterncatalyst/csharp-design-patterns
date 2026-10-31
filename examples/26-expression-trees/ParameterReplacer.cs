using System.Linq.Expressions;

namespace TariffExpressions;

/// <summary>
/// Rebinds every occurrence of one <see cref="ParameterExpression"/> in a
/// tree onto another, so two trees that were each written against their own
/// parameter can be spliced into a single lambda over a shared one.
/// </summary>
public sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _from;
    private readonly ParameterExpression _to;

    public ParameterReplacer(ParameterExpression from, ParameterExpression to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        _from = from;
        _to = to;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node == _from ? _to : base.VisitParameter(node);
    }
}
