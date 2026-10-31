using Xunit;

namespace TariffRules.Tests;

public class TariffEvaluatorTests
{
    private static readonly Shipment Shipment = new(WeightKilograms: 10m, DeclaredValue: 500m, Destination: "Oslo");

    [Fact]
    public void Pattern_match_evaluates_a_nested_tree()
    {
        TariffRule tree = new Surcharge(
            new Sum([new FlatFee(50m), new PerKilogram(5m)]),
            Percent: 10m);

        var total = TariffEvaluator.Evaluate(tree, Shipment);

        Assert.Equal(110m, total);
    }

    public static TheoryData<TariffRule, Shipment, decimal> RuleKinds()
    {
        var shipment = new Shipment(WeightKilograms: 5m, DeclaredValue: 100m, Destination: "Oslo");

        return new TheoryData<TariffRule, Shipment, decimal>
        {
            { new FlatFee(20m), shipment, 20m },
            { new PerKilogram(3m), shipment, 15m },
            { new Surcharge(new FlatFee(10m), Percent: 50m), shipment, 15m },
            { new Sum([new FlatFee(5m), new PerKilogram(2m)]), shipment, 15m },
        };
    }

    [Theory]
    [MemberData(nameof(RuleKinds))]
    public void Pattern_match_handles_each_rule_kind(TariffRule rule, Shipment shipment, decimal expected)
    {
        var total = TariffEvaluator.Evaluate(rule, shipment);

        Assert.Equal(expected, total);
    }
}
