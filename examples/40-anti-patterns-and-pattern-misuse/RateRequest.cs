namespace FreightRating;

/// <summary>
/// A freight rate request: the declared value of the consignment, its weight
/// in kilograms, and whether the current fuel levy applies. Shared by both
/// the right-sized calculator and the over-abstracted component tower below
/// so the two forms can be proven equivalent over the same inputs.
/// </summary>
public sealed record RateRequest(decimal DeclaredValue, decimal WeightKilograms, bool FuelLevyApplies);
