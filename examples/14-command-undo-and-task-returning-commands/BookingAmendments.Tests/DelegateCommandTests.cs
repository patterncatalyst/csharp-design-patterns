using Xunit;

namespace BookingAmendments.Tests;

/// <summary>
/// Proves <see cref="DelegateCommand"/> parity with the interface form: the
/// same change, expressed as a pair of closures instead of a named class,
/// executes and undoes equivalently.
/// </summary>
public class DelegateCommandTests
{
    [Fact]
    public void Delegate_command_executes_and_undoes_equivalently_to_the_interface_form()
    {
        var interfaceBooking = new Booking("BK-5001", "Standard", 500m);
        var interfaceCommand = new ChangeServiceLevelCommand(interfaceBooking, "Express");

        var delegateBooking = new Booking("BK-5001", "Standard", 500m);
        string? priorServiceLevel = null;
        var delegateCommand = new DelegateCommand(
            execute: () =>
            {
                priorServiceLevel = delegateBooking.ServiceLevel;
                delegateBooking.ServiceLevel = "Express";
            },
            undo: () => delegateBooking.ServiceLevel = priorServiceLevel!);

        interfaceCommand.Execute();
        delegateCommand.Execute();

        Assert.Equal(interfaceBooking.ServiceLevel, delegateBooking.ServiceLevel);
        Assert.Equal("Express", delegateBooking.ServiceLevel);

        interfaceCommand.Undo();
        delegateCommand.Undo();

        Assert.Equal(interfaceBooking.ServiceLevel, delegateBooking.ServiceLevel);
        Assert.Equal("Standard", delegateBooking.ServiceLevel);
    }
}
