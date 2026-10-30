using Microsoft.Extensions.DependencyInjection;

namespace RateComposition;

/// <summary>
/// The composition root for the rating pipeline: the one place that lists
/// which decorator layers wrap <see cref="BaseRateCalculator"/>, and in what
/// order, instead of a consumer calling <c>new</c> on each layer itself.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BaseRateCalculator"/> and, from it, the fully
    /// wrapped <see cref="IRateCalculator"/> pipeline: discount, then
    /// surcharge, then tax outermost. Resolving <see cref="IRateCalculator"/>
    /// from the container always returns the complete chain; no consumer
    /// assembles it by hand.
    /// </summary>
    public static IServiceCollection AddRatePipeline(this IServiceCollection services)
    {
        services.AddSingleton(_ => new BaseRateCalculator(ratePerKilogram: 5.00m));

        services.AddSingleton<IRateCalculator>(provider => new TaxRateDecorator(
            new SurchargeRateDecorator(
                new DiscountRateDecorator(provider.GetRequiredService<BaseRateCalculator>(), rate: 0.10m),
                rate: 0.05m),
            rate: 0.08m));

        return services;
    }
}
