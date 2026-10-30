using Microsoft.Extensions.DependencyInjection;

namespace RateQuoting;

/// <summary>
/// The composition root for rate-quoting wiring: the one place that lists
/// which <see cref="IPricingStrategy"/> backs each service-level key.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the three tariffs as keyed singletons (<c>AddKeyedScoped</c>
    /// or <c>AddKeyedTransient</c> would read identically; these strategies
    /// are stateless, so a shared singleton per key is enough) and the two
    /// keyed-service consumers, <see cref="KeyedTariffQuoter"/> and
    /// <see cref="ExpressLaneQuoter"/>.
    /// </summary>
    public static IServiceCollection AddRateQuoting(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IPricingStrategy, StandardTariff>("standard");
        services.AddKeyedSingleton<IPricingStrategy, ExpressTariff>("express");
        services.AddKeyedSingleton<IPricingStrategy, EconomyTariff>("economy");

        services.AddSingleton<KeyedTariffQuoter>();
        services.AddSingleton<ExpressLaneQuoter>();

        return services;
    }
}
