namespace ShipmentWorkflow;

/// <summary>
/// Allocates slots in order, starting from <c>SLOT-001</c>, counting up once
/// per call for the lifetime of the instance.
/// </summary>
public sealed class SequentialSlotAllocator : ISlotAllocator
{
    private int _next = 1;

    public string Allocate(string consignmentId)
    {
        ArgumentNullException.ThrowIfNull(consignmentId);
        return $"SLOT-{_next++:D3}";
    }
}
