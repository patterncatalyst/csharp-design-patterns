using System.Linq.Expressions;

namespace TariffExpressions;

/// <summary>
/// Combines two independently authored rate predicates into one, over a
/// single shared parameter, by rebinding the right-hand predicate's
/// parameter onto the left-hand predicate's before joining their bodies with
/// a short-circuiting <c>&amp;&amp;</c>.
/// </summary>
public static class PredicateComposer
{
    public static Expression<Func<RateRequest, bool>> And(
        Expression<Func<RateRequest, bool>> left,
        Expression<Func<RateRequest, bool>> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var parameter = left.Parameters[0];
        var replacer = new ParameterReplacer(right.Parameters[0], parameter);
        var rightBody = replacer.Visit(right.Body)!;
        var body = Expression.AndAlso(left.Body, rightBody);
        return Expression.Lambda<Func<RateRequest, bool>>(body, parameter);
    }
}
