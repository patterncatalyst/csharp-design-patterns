namespace RateComposition;

/// <summary>
/// A carrier's quoted rate for a shipment: the price and the promised
/// transit time.
/// </summary>
public sealed record CarrierRate(Money Amount, int TransitDays);
