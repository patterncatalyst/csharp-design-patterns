namespace ConcurrentHandling;

/// <summary>
/// Confirms that a <see cref="HandlingEvent"/> was durably recorded, carrying
/// the ledger-assigned sequence number back to the caller.
/// </summary>
public sealed record HandlingReceipt(TrackingId Tracking, int Sequence);
