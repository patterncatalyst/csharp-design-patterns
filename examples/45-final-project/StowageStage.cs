namespace ShipmentWorkflow;

/// <summary>
/// Assigns the booking a cargo slot through the injected
/// <see cref="ISlotAllocator"/> and records the result on the context.
/// </summary>
public sealed class StowageStage(ISlotAllocator allocator) : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var consignmentId = context.Booking.ConsignmentId;
        var stowage = new StowageAssignment(consignmentId, allocator.Allocate(consignmentId));
        return context with { Stowage = stowage };
    }
}
