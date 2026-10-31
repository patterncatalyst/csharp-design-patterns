namespace TrackingHistory;

/// <summary>One scan of a consignment at a carrier checkpoint.</summary>
public sealed record TrackingEvent(string ConsignmentId, TrackingStage Stage, DateTimeOffset OccurredAt, string Location);
