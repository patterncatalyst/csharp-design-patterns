using System.Linq.Expressions;

namespace TariffExpressions;

/// <summary>
/// Walks a rule's expression tree and records which <see cref="RateRequest"/>
/// properties it touches, without compiling or invoking the rule. A rule that
/// would throw if it were ever run, such as a division by a literal zero,
/// collects cleanly because visiting a tree never evaluates it.
/// </summary>
public sealed class ReferencedPropertyCollector : ExpressionVisitor
{
    private readonly HashSet<string> _properties = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Collect(Expression rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _properties.Clear();
        Visit(rule);
        return _properties.ToArray();
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Expression is ParameterExpression && node.Member.DeclaringType == typeof(RateRequest))
        {
            _properties.Add(node.Member.Name);
        }

        return base.VisitMember(node);
    }
}
