namespace TrackingHistory;

/// <summary>One page of <see cref="TrackingEvent"/>s, numbered from 1, as produced by <see cref="ConsignmentHistory.Paginate"/>.</summary>
public sealed record TrackingPage(int PageNumber, IReadOnlyList<TrackingEvent> Events);
