namespace RateComposition;

/// <summary>
/// A caching Proxy in front of an <see cref="ICarrierRateGateway"/>: the
/// first call for a given <see cref="RateRequest"/> forwards to
/// <paramref name="inner"/> and remembers the answer; every later call for
/// an equal request returns the remembered <see cref="CarrierRate"/> without
/// calling <paramref name="inner"/> again. <see cref="RateRequest"/> is a
/// record, so two requests with the same origin, destination, and weight
/// are equal and share one cache entry.
/// </summary>
public sealed class CachingRateGatewayProxy(ICarrierRateGateway inner) : ICarrierRateGateway
{
    private readonly Dictionary<RateRequest, CarrierRate> cache = [];

    public CarrierRate GetRate(RateRequest request)
    {
        if (cache.TryGetValue(request, out var cached))
        {
            return cached;
        }

        var rate = inner.GetRate(request);
        cache[request] = rate;

        return rate;
    }
}
