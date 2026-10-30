using Xunit;

namespace BookingAmendments.Tests;

/// <summary>
/// Proves the core reversible-command contract: executing applies the
/// change, and undoing puts the booking back exactly as it was.
/// </summary>
public class ChangeServiceLevelCommandTests
{
    [Fact]
    public void Execute_then_undo_restores_the_prior_service_level()
    {
        var booking = new Booking("BK-1001", "Standard", 500m);
        var command = new ChangeServiceLevelCommand(booking, "Express");

        command.Execute();

        Assert.Equal("Express", booking.ServiceLevel);

        command.Undo();

        Assert.Equal("Standard", booking.ServiceLevel);
    }

    [Fact]
    public void Undo_before_execute_throws_because_there_is_nothing_to_restore()
    {
        var booking = new Booking("BK-1002", "Standard", 500m);
        var command = new ChangeServiceLevelCommand(booking, "Express");

        Assert.Throws<InvalidOperationException>(command.Undo);
    }
}
