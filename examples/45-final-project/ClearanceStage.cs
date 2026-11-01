namespace ShipmentWorkflow;

/// <summary>
/// Checks the booking through the injected <see cref="IClearanceCheck"/> and
/// records the result on the context. <see cref="ShipmentPipeline"/> stops
/// before invoicing when the result is not cleared.
/// </summary>
public sealed class ClearanceStage(IClearanceCheck check) : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var clearance = check.Check(context.Booking);
        return context with { Clearance = clearance };
    }
}
