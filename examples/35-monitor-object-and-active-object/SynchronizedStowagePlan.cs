namespace StowageCoordination;

/// <summary>
/// STAGE 1 - Monitor Object. A single gate serializes every call into the
/// servant. The body is entirely synchronous, so the gate is held for the
/// shortest possible time and there is no <c>await</c> anywhere near the
/// lock.
/// </summary>
public sealed class SynchronizedStowagePlan
{
    private readonly StowagePlan _plan;
    private readonly Lock _gate = new();

    public SynchronizedStowagePlan(StowagePlan plan) => _plan = plan;

    public SlotAssignment? Reserve(ConsignmentId id, SlotRequirements req)
    {
        lock (_gate)
        {
            return _plan.TryReserve(id, req);
        }
    }
}
