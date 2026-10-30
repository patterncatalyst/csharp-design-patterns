using Xunit;

namespace ConsignmentTracking.Tests;

/// <summary>
/// Proves the transition table itself: a legal (status, event) pair advances
/// to the right next status, and anything not in the table throws
/// <see cref="InvalidTransitionException"/> instead of silently doing nothing.
/// </summary>
public class TransitionsTests
{
    [Fact]
    public void Booked_and_Load_advances_to_Loaded()
    {
        var next = Transitions.Next(ConsignmentStatus.Booked, HandlingEventType.Load);

        Assert.Equal(ConsignmentStatus.Loaded, next);
    }

    [Fact]
    public void Booked_and_Deliver_throws_InvalidTransitionException()
    {
        Assert.Throws<InvalidTransitionException>(() =>
            Transitions.Next(ConsignmentStatus.Booked, HandlingEventType.Deliver));
    }
}
