namespace RateEngineReplacement;

/// <summary>
/// A request to quote a shipping rate for one consignment: its declared value
/// for the ad-valorem portion of the rate, its weight for the per-kilogram
/// portion, and the lane it travels for the tariff lookup. Shared by the
/// legacy engine and both of its replacements so all three are compared on
/// identical input.
/// </summary>
public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, string LaneCode);
