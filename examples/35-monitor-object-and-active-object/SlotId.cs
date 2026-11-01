namespace StowageCoordination;

/// <summary>
/// Identifies a single stowage slot aboard a vessel by bay, row, and tier.
/// Tier 1 slots are powered (reefer-capable); other tiers are not.
/// </summary>
public readonly record struct SlotId(int Bay, int Row, int Tier);
