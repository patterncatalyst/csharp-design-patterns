namespace ShipmentWorkflow;

/// <summary>
/// The outcome of a customs clearance check: either cleared, or rejected with
/// a reason. <see cref="ShipmentPipeline"/> stops before invoicing when a
/// booking is rejected.
/// </summary>
public sealed record ClearanceResult(bool Cleared, string? Reason)
{
    public static ClearanceResult Clear() => new(true, null);

    public static ClearanceResult Rejected(string reason) => new(false, reason);
}
