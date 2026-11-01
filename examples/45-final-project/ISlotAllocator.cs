namespace ShipmentWorkflow;

/// <summary>
/// Allocates a cargo slot for a consignment. Substituted in tests so
/// <see cref="StowageStage"/> can be verified without depending on the real
/// allocation sequence.
/// </summary>
public interface ISlotAllocator
{
    string Allocate(string consignmentId);
}
