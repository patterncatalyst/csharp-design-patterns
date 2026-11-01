namespace RateEngineCatalogues;

/// <summary>
/// A request to quote a shipping rate for one consignment: its declared value
/// for the ad-valorem portion of the rate, its weight for the per-kilogram
/// portion, and the service level that selects the surcharge. Shared by both
/// the <see cref="RateEngineCatalogues.RateEngine"/> and
/// <see cref="RateEngineCatalogues.SeamedRateEngine"/> forms so the two are
/// compared on identical input.
/// </summary>
public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, ServiceLevel Service);
