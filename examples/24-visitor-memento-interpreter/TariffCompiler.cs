using System.Linq.Expressions;

namespace TariffRules;

/// <summary>
/// The Interpreter pattern as the GoF described it: a <see cref="TariffRule"/>
/// expression tree is walked once to build a runtime expression tree via
/// <see cref="System.Linq.Expressions"/>, which is then compiled to a plain
/// delegate. Compiling once and invoking the delegate many times amortizes the
/// interpretation cost that a tree-walking evaluator pays on every call.
/// </summary>
public static class TariffCompiler
{
    public static Func<Shipment, decimal> Compile(TariffRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        ParameterExpression shipment = Expression.Parameter(typeof(Shipment), "shipment");
        return Expression.Lambda<Func<Shipment, decimal>>(Build(rule, shipment), shipment).Compile();
    }

    private static Expression Build(TariffRule rule, ParameterExpression shipment) => rule switch
    {
        FlatFee f => Expression.Constant(f.Amount),
        PerKilogram p => Expression.Multiply(
            Expression.Constant(p.Rate),
            Expression.Property(shipment, nameof(Shipment.WeightKilograms))),
        Surcharge s => Expression.Multiply(
            Build(s.Inner, shipment),
            Expression.Constant(1 + s.Percent / 100m)),
        Sum s => s.Rules.Aggregate(
            (Expression)Expression.Constant(0m),
            (accumulator, component) => Expression.Add(accumulator, Build(component, shipment))),
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };
}
