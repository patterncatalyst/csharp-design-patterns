namespace ShipmentWorkflow;

/// <summary>
/// The composition root for a <see cref="ShipmentPipeline"/>: wires the
/// default stages and their collaborators with plain <c>new</c>, no
/// container.
/// </summary>
public static class ShipmentPipelineFactory
{
    public static ShipmentPipeline CreateDefault() => new(
    [
        new RatingStage(),
        new StowageStage(new SequentialSlotAllocator()),
        new ClearanceStage(new StandardClearanceCheck()),
        new InvoiceStage(),
    ]);
}
