namespace RateComposition;

/// <summary>
/// Looks up a carrier's quoted rate for a <see cref="RateRequest"/>.
/// <see cref="SlowCarrierRateGateway"/> is the real, expensive-to-call
/// implementation; <see cref="CachingRateGatewayProxy"/> implements the same
/// interface and sits in front of it, so a caller holding an
/// <see cref="ICarrierRateGateway"/> cannot tell whether it is talking to the
/// carrier directly or to a cache.
/// </summary>
public interface ICarrierRateGateway
{
    CarrierRate GetRate(RateRequest request);
}
