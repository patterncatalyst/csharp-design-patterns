using System.Reflection;
using FreightRating.OverAbstracted;
using Xunit;

namespace FreightRating.Tests;

public class RateCalculatorTests
{
    [Fact]
    public void Quote_sums_base_rate_and_weight_surcharge()
    {
        var request = new RateRequest(DeclaredValue: 1_000m, WeightKilograms: 100m, FuelLevyApplies: false);

        var quote = RateCalculator.Quote(request);

        Assert.Equal(265m, quote);
    }

    [Fact]
    public void Quote_adds_the_fuel_levy_when_it_applies()
    {
        var request = new RateRequest(DeclaredValue: 1_000m, WeightKilograms: 100m, FuelLevyApplies: true);

        var quote = RateCalculator.Quote(request);

        Assert.Equal(286.20m, quote);
    }

    [Fact]
    public void Quote_omits_the_fuel_levy_when_it_does_not_apply()
    {
        var withLevy = new RateRequest(DeclaredValue: 500m, WeightKilograms: 50m, FuelLevyApplies: true);
        var withoutLevy = withLevy with { FuelLevyApplies = false };

        var levyQuote = RateCalculator.Quote(withLevy);
        var noLevyQuote = RateCalculator.Quote(withoutLevy);

        Assert.True(levyQuote > noLevyQuote);
    }

    [Fact]
    public void Quote_returns_zero_for_an_empty_request()
    {
        var request = new RateRequest(DeclaredValue: 0m, WeightKilograms: 0m, FuelLevyApplies: false);

        var quote = RateCalculator.Quote(request);

        Assert.Equal(0m, quote);
    }

    [Fact]
    public void Quote_is_a_pure_calculation()
    {
        var request = new RateRequest(DeclaredValue: 750m, WeightKilograms: 220m, FuelLevyApplies: true);

        var first = RateCalculator.Quote(request);
        var second = RateCalculator.Quote(request);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Collapsed_calculator_exposes_a_single_public_quote_method()
    {
        var publicStaticMethods = typeof(RateCalculator).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.Single(publicStaticMethods);
    }

    [Fact]
    public void Rate_engine_sums_its_components()
    {
        var engine = new RateEngine(new StandardRateComponentFactory());
        var request = new RateRequest(DeclaredValue: 1_000m, WeightKilograms: 100m, FuelLevyApplies: false);

        var quote = engine.Quote(request);

        Assert.Equal(265m, quote);
    }

    [Fact]
    public void Rate_engine_applies_the_fuel_levy_component_on_the_running_subtotal()
    {
        var engine = new RateEngine(new StandardRateComponentFactory());
        var request = new RateRequest(DeclaredValue: 1_000m, WeightKilograms: 100m, FuelLevyApplies: true);

        var quote = engine.Quote(request);

        Assert.Equal(286.20m, quote);
    }

    [Fact]
    public void Standard_factory_creates_the_three_rate_components_in_order()
    {
        var factory = new StandardRateComponentFactory();

        var components = factory.CreateComponents();

        Assert.Collection(
            components,
            component => Assert.IsType<BaseRateComponent>(component),
            component => Assert.IsType<WeightSurchargeComponent>(component),
            component => Assert.IsType<FuelLevyComponent>(component));
    }
}
