using Microsoft.Extensions.DependencyInjection;

namespace CarrierGateways;

/// <summary>
/// The composition root for carrier gateway wiring: the one place that
/// knows about a concrete <see cref="ICarrierGateway"/> implementation.
/// Every consumer in this library resolves <see cref="ICarrierGateway"/>,
/// <see cref="ICarrierRates"/>, or <see cref="ICarrierTracking"/> from the
/// built-in container and never names <see cref="RestCarrierGateway"/> or
/// <see cref="FakeCarrierGateway"/> directly.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the production carrier gateway, <see cref="RestCarrierGateway"/>.
    /// </summary>
    public static IServiceCollection AddCarrierGateway(this IServiceCollection services) =>
        services.AddCarrierGateway<RestCarrierGateway>();

    /// <summary>
    /// Registers <typeparamref name="TGateway"/> as the carrier gateway and
    /// exposes its two Interface-Segregated facets, <see cref="ICarrierRates"/>
    /// and <see cref="ICarrierTracking"/>, as independently resolvable
    /// abstractions backed by the same singleton instance. Swapping the
    /// gateway implementation used across the whole application, for
    /// example from <see cref="RestCarrierGateway"/> to
    /// <see cref="FakeCarrierGateway"/> for a sandbox environment, is one
    /// call-site change here; no consumer's constructor changes.
    /// </summary>
    public static IServiceCollection AddCarrierGateway<TGateway>(this IServiceCollection services)
        where TGateway : class, ICarrierGateway
    {
        services.AddSingleton<TGateway>();
        services.AddSingleton<ICarrierGateway>(provider => provider.GetRequiredService<TGateway>());
        services.AddSingleton<ICarrierRates>(provider => provider.GetRequiredService<TGateway>());
        services.AddSingleton<ICarrierTracking>(provider => provider.GetRequiredService<TGateway>());

        // Both consumers take constructor dependencies; the container builds
        // ShipmentRateAdvisor from ICarrierRates alone (ISP) and
        // ShipmentBookingService from the full ICarrierGateway (DIP).
        services.AddSingleton<ShipmentRateAdvisor>();
        services.AddSingleton<ShipmentBookingService>();

        return services;
    }
}
