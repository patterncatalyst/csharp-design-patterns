using System.Linq.Expressions;

namespace TariffExpressions;

/// <summary>
/// Tariff rules authored as <b>data</b>, not as compiled code. Each property
/// is an <see cref="Expression{TDelegate}"/>, a tree of <see cref="Expression"/>
/// nodes describing a computation, which the rest of this example inspects,
/// rewrites, and only compiles at the point it actually needs to run a rule.
/// </summary>
public static class TariffExpressionLibrary
{
    public static Expression<Func<RateRequest, decimal>> WeightRate => r => r.WeightKilograms * 2.50m;

    public static Expression<Func<RateRequest, decimal>> ValueRate => r => r.DeclaredValue * 0.015m;

    public static Expression<Func<RateRequest, bool>> IsHeavy => r => r.WeightKilograms > 20m;
}
