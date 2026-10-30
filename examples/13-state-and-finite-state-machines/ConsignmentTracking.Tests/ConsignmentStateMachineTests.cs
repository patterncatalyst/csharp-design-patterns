using Xunit;

namespace ConsignmentTracking.Tests;

/// <summary>
/// Proves the State-pattern context object: <see cref="ConsignmentStateMachine.Apply"/>
/// advances <see cref="ConsignmentStateMachine.CurrentStatus"/> through
/// <see cref="Transitions.Next"/>, rejects illegal events, and builds a
/// <see cref="TrackingHistory"/> that records every step.
/// </summary>
public class ConsignmentStateMachineTests
{
    private static HandlingEvent EventAt(HandlingEventType type, string location) =>
        new(type, DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), location);

    [Fact]
    public void Apply_with_Load_from_Booked_transitions_to_Loaded()
    {
        var machine = new ConsignmentStateMachine();

        machine.Apply(EventAt(HandlingEventType.Load, "Rotterdam"));

        Assert.Equal(ConsignmentStatus.Loaded, machine.CurrentStatus);
    }

    [Fact]
    public void Applying_an_illegal_event_throws_InvalidTransitionException()
    {
        var machine = new ConsignmentStateMachine();

        Assert.Throws<InvalidTransitionException>(() =>
            machine.Apply(EventAt(HandlingEventType.Deliver, "Rotterdam")));
    }

    [Fact]
    public void Full_lifecycle_drives_Booked_through_Delivered_and_records_history()
    {
        var machine = new ConsignmentStateMachine();

        machine.Apply(EventAt(HandlingEventType.Load, "Rotterdam"));
        machine.Apply(EventAt(HandlingEventType.Depart, "Rotterdam"));
        machine.Apply(EventAt(HandlingEventType.ClearCustoms, "Santos"));
        machine.Apply(EventAt(HandlingEventType.Deliver, "Santos"));

        Assert.Equal(ConsignmentStatus.Delivered, machine.CurrentStatus);
        Assert.Equal(4, machine.History.Records.Count);
        Assert.Equal(
            new[] { ConsignmentStatus.Loaded, ConsignmentStatus.InTransit, ConsignmentStatus.Cleared, ConsignmentStatus.Delivered },
            machine.History.Records.Select(record => record.Resulting));
    }
}
