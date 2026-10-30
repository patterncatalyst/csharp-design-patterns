using Microsoft.Extensions.DependencyInjection;

namespace RateQuoting;

/// <summary>
/// Tier 3: Strategy selected by the built-in DI container's keyed services.
/// Rather than branching on <see cref="RateRequest.ServiceLevel"/> with an
/// if/else or a dictionary this class owns, the container resolves the
/// matching <see cref="IPricingStrategy"/> by that string key at the
/// registration in <see cref="ServiceCollectionExtensions.AddRateQuoting"/>.
/// Adding a fourth service level is a new keyed registration there; this
/// class does not change.
/// </summary>
public sealed class KeyedTariffQuoter(IServiceProvider serviceProvider)
{
    public RateQuote Quote(RateRequest request)
    {
        var strategy = serviceProvider.GetRequiredKeyedService<IPricingStrategy>(request.ServiceLevel);
        var price = strategy.Price(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}

/// <summary>
/// A second keyed-service consumer, resolved from the container with
/// <see cref="FromKeyedServicesAttribute"/> instead of looking a key up at
/// request time: this class always prices with the "express" tariff, chosen
/// once at construction rather than per call.
/// </summary>
public sealed class ExpressLaneQuoter([FromKeyedServices("express")] IPricingStrategy expressStrategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = expressStrategy.Price(request);
        return new RateQuote(request.Destination, price, "express");
    }
}
