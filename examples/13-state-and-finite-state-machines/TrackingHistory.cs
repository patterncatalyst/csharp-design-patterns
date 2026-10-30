namespace ConsignmentTracking;

/// <summary>
/// The ordered audit trail a <see cref="ConsignmentStateMachine"/> builds as it
/// applies <see cref="HandlingEvent"/>s. Exposed read-only so a consumer can
/// inspect a consignment's journey without being able to rewrite it.
/// </summary>
public sealed class TrackingHistory
{
    private readonly List<HandlingRecord> _records = [];

    public IReadOnlyList<HandlingRecord> Records => _records;

    public void Append(HandlingRecord record) => _records.Add(record);
}
