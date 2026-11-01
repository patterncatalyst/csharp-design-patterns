namespace StowageCoordination;

/// <summary>
/// What a consignment needs from the slot it is assigned: refrigeration,
/// hazardous-cargo separation, and gross weight.
/// </summary>
public sealed record SlotRequirements(bool Reefer, bool Hazardous, int WeightKg);
