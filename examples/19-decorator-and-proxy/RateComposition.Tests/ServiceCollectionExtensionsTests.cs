using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RateComposition.Tests;

/// <summary>
/// Proves the DI-decoration shape: resolving <see cref="IRateCalculator"/>
/// from a container configured with <c>AddRatePipeline</c> returns the same
/// fully wrapped chain a caller would otherwise have to assemble by hand,
/// not the bare <see cref="BaseRateCalculator"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    private static readonly RateRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void AddRatePipeline_resolves_a_fully_wrapped_calculator_matching_the_hand_built_chain()
    {
        var services = new ServiceCollection().AddRatePipeline();
        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IRateCalculator>();

        var expected = new TaxRateDecorator(
            new SurchargeRateDecorator(
                new DiscountRateDecorator(new BaseRateCalculator(5.00m), rate: 0.10m),
                rate: 0.05m),
            rate: 0.08m);

        Assert.Equal(expected.Quote(Request), resolved.Quote(Request));
    }

    [Fact]
    public void AddRatePipeline_resolved_calculator_is_the_outermost_tax_decorator_not_the_bare_base()
    {
        var services = new ServiceCollection().AddRatePipeline();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IRateCalculator>();

        Assert.IsType<TaxRateDecorator>(resolved);
        Assert.IsNotType<BaseRateCalculator>(resolved);
    }

    [Fact]
    public void AddRatePipeline_registers_the_base_calculator_as_its_own_resolvable_service_too()
    {
        var services = new ServiceCollection().AddRatePipeline();
        using var provider = services.BuildServiceProvider();

        var baseCalculator = provider.GetRequiredService<BaseRateCalculator>();
        var pipeline = provider.GetRequiredService<IRateCalculator>();

        Assert.NotEqual(baseCalculator.Quote(Request), pipeline.Quote(Request));
    }
}
