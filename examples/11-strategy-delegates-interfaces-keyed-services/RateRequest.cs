namespace RateQuoting;

/// <summary>
/// A request to price a shipment. <see cref="ServiceLevel"/> doubles as the
/// keyed-service lookup key at the keyed-service tier: "standard",
/// "express", or "economy".
/// </summary>
public sealed record RateRequest(string Destination, decimal WeightKilograms, string ServiceLevel);
