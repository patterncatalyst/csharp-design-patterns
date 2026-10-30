namespace ConsignmentTracking;

/// <summary>
/// The State pattern's context object: tracks one consignment's current
/// <see cref="ConsignmentStatus"/> and the <see cref="TrackingHistory"/> of how
/// it got there, delegating the actual transition logic to
/// <see cref="Transitions.Next"/> instead of branching on status itself.
/// </summary>
public sealed class ConsignmentStateMachine
{
    /// <summary>
    /// The consignment's current status. Starts at the enum's default member,
    /// <see cref="ConsignmentStatus.Booked"/>, since every consignment begins
    /// booked before it is ever loaded.
    /// </summary>
    public ConsignmentStatus CurrentStatus { get; private set; }

    public TrackingHistory History { get; } = new();

    /// <summary>
    /// Applies a carrier's <see cref="HandlingEvent"/>, advancing
    /// <see cref="CurrentStatus"/> via <see cref="Transitions.Next"/> and
    /// recording the result in <see cref="History"/>. Throws
    /// <see cref="InvalidTransitionException"/> if the event has no legal
    /// transition from the current status.
    /// </summary>
    public void Apply(HandlingEvent handlingEvent)
    {
        ArgumentNullException.ThrowIfNull(handlingEvent);

        var resulting = Transitions.Next(CurrentStatus, handlingEvent.Type);
        CurrentStatus = resulting;
        History.Append(new HandlingRecord(handlingEvent, resulting));
    }
}
