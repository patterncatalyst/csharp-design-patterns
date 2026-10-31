namespace CarrierDispatch;

/// <summary>
/// Proof that a carrier accepted a consignment for dispatch.
/// </summary>
public sealed record DispatchReceipt(string ConsignmentId, string TrackingNumber, DateTimeOffset AcceptedAt);
