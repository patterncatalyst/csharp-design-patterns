namespace StowageCoordination;

/// <summary>A confirmed binding of one consignment to one stowage slot.</summary>
public sealed record SlotAssignment(ConsignmentId Consignment, SlotId Slot);
