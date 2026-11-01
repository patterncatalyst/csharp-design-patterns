namespace RateEngineSeams;

/// <summary>
/// A request to quote a shipping rate for one consignment: its declared value
/// for the ad-valorem portion of the rate, and its weight for the per-kilogram
/// portion. Shared by both the legacy and the refactored <c>RateEngine</c> so
/// the two forms are compared on identical input.
/// </summary>
public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms);
