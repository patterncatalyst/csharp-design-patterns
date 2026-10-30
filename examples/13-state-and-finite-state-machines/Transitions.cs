namespace ConsignmentTracking;

/// <summary>
/// The consignment lifecycle's transition table: the GoF State pattern
/// expressed as data instead of a class per state. Every legal
/// (<see cref="ConsignmentStatus"/>, <see cref="HandlingEventType"/>) pair is
/// one switch-expression arm; anything not listed is a bug in the caller, not
/// a state to model, so the catch-all arm throws rather than silently no-op.
/// </summary>
public static class Transitions
{
    public static ConsignmentStatus Next(ConsignmentStatus current, HandlingEventType evt) => (current, evt) switch
    {
        (ConsignmentStatus.Booked, HandlingEventType.Load) => ConsignmentStatus.Loaded,
        (ConsignmentStatus.Loaded, HandlingEventType.Depart) => ConsignmentStatus.InTransit,
        (ConsignmentStatus.InTransit, HandlingEventType.ClearCustoms) => ConsignmentStatus.Cleared,
        (ConsignmentStatus.Cleared, HandlingEventType.Deliver) => ConsignmentStatus.Delivered,
        _ => throw new InvalidTransitionException(
            $"Cannot apply '{evt}' while a consignment is '{current}'."),
    };
}
