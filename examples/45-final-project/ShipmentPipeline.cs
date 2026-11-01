namespace ShipmentWorkflow;

/// <summary>
/// Runs a booking through an ordered list of <see cref="IShipmentStage"/>
/// instances, stopping before invoicing if clearance rejects the booking.
/// Composed by <see cref="ShipmentPipelineFactory.CreateDefault"/> with no
/// container involved.
/// </summary>
public sealed class ShipmentPipeline(IReadOnlyList<IShipmentStage> stages)
{
    public ShipmentContext Run(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new ShipmentContext(request);
        foreach (var stage in stages)
        {
            context = stage.Process(context);
            if (context.Clearance is { Cleared: false })
            {
                return context;
            }
        }

        return context;
    }
}
