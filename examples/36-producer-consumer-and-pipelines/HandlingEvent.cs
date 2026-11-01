namespace HandlingEventIngest;

/// <summary>
/// A single handling report submitted into the pipeline: which consignment,
/// what kind of handling occurred, and the reporting carrier's own sequence
/// number for that consignment. <see cref="Sequence"/> is what lets a test
/// prove a single-consumer pipeline preserves submission order.
/// </summary>
public sealed record HandlingEvent(TrackingId Tracking, HandlingKind Kind, int Sequence);
