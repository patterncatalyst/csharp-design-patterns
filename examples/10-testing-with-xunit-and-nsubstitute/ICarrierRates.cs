namespace RatingEngine;

/// <summary>
/// Looks up a carrier's base rate for moving a consignment's weight to a
/// destination. Stands in for a real network call to a carrier's rate API, so
/// tests substitute it rather than calling out over the network.
/// </summary>
public interface ICarrierRates
{
    Task<Money> GetBaseRateAsync(string destination, decimal kilograms, CancellationToken cancellationToken);
}
