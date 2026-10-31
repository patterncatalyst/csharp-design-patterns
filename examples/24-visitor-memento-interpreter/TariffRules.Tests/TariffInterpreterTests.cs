using Xunit;

namespace TariffRules.Tests;

public class TariffInterpreterTests
{
    private static readonly Shipment Shipment = new(WeightKilograms: 10m, DeclaredValue: 500m, Destination: "Rotterdam");

    [Fact]
    public void Compiled_flat_fee_returns_the_fee()
    {
        var rule = new FlatFee(25m);

        var compiled = TariffCompiler.Compile(rule);

        Assert.Equal(25m, compiled(Shipment));
    }

    [Fact]
    public void Compiled_per_kilogram_scales_with_weight()
    {
        var rule = new PerKilogram(4m);

        var compiled = TariffCompiler.Compile(rule);

        Assert.Equal(40m, compiled(Shipment));
    }

    [Fact]
    public void Compiled_surcharge_applies_percent_over_inner()
    {
        var rule = new Surcharge(new FlatFee(100m), Percent: 10m);

        var compiled = TariffCompiler.Compile(rule);

        Assert.Equal(110m, compiled(Shipment));
    }

    [Fact]
    public void Compiled_sum_adds_component_rules()
    {
        var rule = new Sum([new FlatFee(10m), new PerKilogram(2m)]);

        var compiled = TariffCompiler.Compile(rule);

        Assert.Equal(30m, compiled(Shipment));
    }

    [Fact]
    public void Compiled_delegate_matches_the_pattern_matching_evaluator()
    {
        TariffRule tree = new Sum(
        [
            new Surcharge(new PerKilogram(3m), Percent: 20m),
            new FlatFee(15m),
        ]);

        var compiled = TariffCompiler.Compile(tree);

        Assert.Equal(TariffEvaluator.Evaluate(tree, Shipment), compiled(Shipment));
    }
}
