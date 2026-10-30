namespace RateComposition;

/// <summary>
/// A request to rate a shipment between two locations. Every calculator,
/// decorator, and gateway in this example prices or quotes against the same
/// <see cref="RateRequest"/>.
/// </summary>
public sealed record RateRequest(string Origin, string Destination, decimal Kilograms);
