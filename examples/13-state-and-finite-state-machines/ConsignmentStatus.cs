namespace ConsignmentTracking;

/// <summary>
/// The finite set of states a consignment can occupy. <see cref="Booked"/> is
/// the start state every <see cref="ConsignmentStateMachine"/> and
/// <see cref="ProbabilisticTransitMachine"/> begins in.
/// </summary>
public enum ConsignmentStatus
{
    Booked,
    Loaded,
    InTransit,
    Cleared,
    Delivered,
    Delayed,
}
