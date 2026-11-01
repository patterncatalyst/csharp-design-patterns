namespace StowageCoordination;

/// <summary>
/// THE SERVANT: plain, single-threaded stowage logic, deliberately not
/// thread-safe. <see cref="TryReserve"/> is a non-atomic read-modify-write
/// over <c>_assigned</c>, so handing it to concurrent callers without a
/// coordination strategy wrapped around it can double-allocate a slot. Every
/// stage in this example wraps the same servant in a different coordination
/// strategy rather than changing this class.
/// </summary>
public sealed class StowagePlan
{
    private readonly Dictionary<SlotId, ConsignmentId> _assigned = [];
    private readonly IReadOnlyList<SlotId> _slots;

    public StowagePlan(IReadOnlyList<SlotId> slots) => _slots = slots;

    public SlotAssignment? TryReserve(ConsignmentId id, SlotRequirements req)
    {
        foreach (var slot in _slots)
        {
            if (_assigned.ContainsKey(slot)) continue;
            if (req.Reefer && slot.Tier != 1) continue;
            _assigned[slot] = id;
            return new SlotAssignment(id, slot);
        }

        return null;
    }

    public int Count => _assigned.Count;
}
