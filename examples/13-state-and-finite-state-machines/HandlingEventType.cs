namespace ConsignmentTracking;

/// <summary>
/// The events a carrier reports against a consignment. Each legal
/// <see cref="ConsignmentStatus"/> transition in <see cref="Transitions"/> is
/// keyed by one of these alongside the current status.
/// </summary>
public enum HandlingEventType
{
    Load,
    Depart,
    ClearCustoms,
    Deliver,
}
