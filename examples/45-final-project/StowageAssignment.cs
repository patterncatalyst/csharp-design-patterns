namespace ShipmentWorkflow;

/// <summary>
/// The cargo slot a consignment is assigned to aboard the vessel, produced by
/// an <see cref="ISlotAllocator"/> inside <see cref="StowageStage"/>.
/// </summary>
public sealed record StowageAssignment(string ConsignmentId, string SlotId);
