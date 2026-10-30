namespace BookingAmendments;

/// <summary>
/// A reversible amendment to a <see cref="Booking"/>. <see cref="Execute"/>
/// applies the change and captures whatever state <see cref="Undo"/> needs to
/// put the booking back exactly as it was.
/// </summary>
public interface IBookingCommand
{
    void Execute();

    void Undo();
}
