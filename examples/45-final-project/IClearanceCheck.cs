namespace ShipmentWorkflow;

/// <summary>
/// Checks whether a booking may clear customs. <see cref="ClearanceStage"/>
/// stops the <see cref="ShipmentPipeline"/> before invoicing when the result
/// is not cleared.
/// </summary>
public interface IClearanceCheck
{
    ClearanceResult Check(BookingRequest request);
}
