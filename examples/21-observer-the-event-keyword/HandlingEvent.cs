namespace ConsignmentTracking;

/// <summary>
/// One scan of a consignment at a location: a status change, where it
/// happened, and when. Both the <see cref="ConsignmentTracker"/> event and
/// the <see cref="HandlingEventStream"/> observable carry this same record,
/// so subscribers on either side see an identical payload.
/// </summary>
public sealed record HandlingEvent(
    string ConsignmentId,
    ConsignmentStatus Status,
    string Location,
    DateTimeOffset OccurredAt);
