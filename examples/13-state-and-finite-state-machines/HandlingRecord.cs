namespace ConsignmentTracking;

/// <summary>
/// One entry in a <see cref="TrackingHistory"/>: the <see cref="HandlingEvent"/>
/// that was applied and the <see cref="ConsignmentStatus"/> it produced.
/// </summary>
public sealed record HandlingRecord(HandlingEvent Event, ConsignmentStatus Resulting);
