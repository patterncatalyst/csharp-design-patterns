namespace BookingAmendments;

/// <summary>
/// Appends a line item to a booking. The item itself, a <see cref="LineItem"/>
/// record, is the snapshot: <see cref="Undo"/> removes the exact value that
/// <see cref="Execute"/> added, nothing else needs to be remembered.
/// </summary>
public sealed class AddLineItemCommand(Booking booking, LineItem lineItem) : IBookingCommand
{
    public void Execute() => booking.AddLineItem(lineItem);

    public void Undo() => booking.RemoveLineItem(lineItem);
}
