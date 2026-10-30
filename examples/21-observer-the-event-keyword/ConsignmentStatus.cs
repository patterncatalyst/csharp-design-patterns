namespace ConsignmentTracking;

/// <summary>
/// The lifecycle a tracked consignment moves through, from first scan to
/// final delivery.
/// </summary>
public enum ConsignmentStatus
{
    Received,
    Loaded,
    InTransit,
    CustomsHold,
    Cleared,
    Delivered,
}
