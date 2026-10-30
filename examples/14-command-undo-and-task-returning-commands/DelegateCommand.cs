namespace BookingAmendments;

/// <summary>
/// The delegate-as-command form: two plain <see cref="Action"/> values stand
/// in for a hand-written <see cref="IBookingCommand"/> implementation when a
/// one-off amendment does not earn its own named class.
/// </summary>
public sealed class DelegateCommand(Action execute, Action undo) : IBookingCommand
{
    public void Execute() => execute();

    public void Undo() => undo();
}
