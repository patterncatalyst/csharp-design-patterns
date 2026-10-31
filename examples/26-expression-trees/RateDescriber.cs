using System.Linq.Expressions;
using System.Text;

namespace TariffExpressions;

/// <summary>
/// Walks a rate rule's expression tree and renders it as a short, readable
/// string, the way a developer would narrate the tree by hand. This never
/// compiles or runs the rule; it only reads the nodes <see cref="Expression"/>
/// already built for it.
/// </summary>
public sealed class RateDescriber : ExpressionVisitor
{
    private readonly StringBuilder _text = new();

    public string Describe(Expression<Func<RateRequest, decimal>> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _text.Clear();
        Visit(rule.Body);
        return _text.ToString();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _text.Append('(');
        Visit(node.Left);
        _text.Append(' ').Append(DescribeOperator(node.NodeType)).Append(' ');
        Visit(node.Right);
        _text.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Expression is ParameterExpression parameter)
        {
            _text.Append(parameter.Name).Append('.').Append(node.Member.Name);
        }
        else
        {
            Visit(node.Expression);
            _text.Append('.').Append(node.Member.Name);
        }

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _text.Append(node.Value);
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _text.Append(node.Name);
        return node;
    }

    private static string DescribeOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Add => "+",
        ExpressionType.Subtract => "-",
        ExpressionType.Multiply => "*",
        ExpressionType.Divide => "/",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.AndAlso => "&&",
        ExpressionType.OrElse => "||",
        _ => nodeType.ToString(),
    };
}
