namespace ShipmentWorkflow;

/// <summary>
/// One step in the <see cref="ShipmentPipeline"/>: reads the
/// <see cref="ShipmentContext"/> built so far and returns the next one.
/// </summary>
public interface IShipmentStage
{
    ShipmentContext Process(ShipmentContext context);
}
