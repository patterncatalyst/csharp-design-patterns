namespace RateEngineDependencies;

/// <summary>
/// A request to rate one consignment moving over a named lane. Shared by both
/// the legacy and the refactored <c>RateEngine</c> so the two forms are
/// compared over identical inputs.
/// </summary>
public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, string LaneCode);
