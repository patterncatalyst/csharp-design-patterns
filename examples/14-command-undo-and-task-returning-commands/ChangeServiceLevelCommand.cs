namespace BookingAmendments;

/// <summary>
/// Changes a booking's service level, snapshotting the prior value so
/// <see cref="Undo"/> restores it exactly. The snapshot is a copied
/// <see cref="string"/> value, never a live reference back into
/// <paramref name="booking"/>, so later commands against the same booking
/// cannot disturb what this command remembers.
/// </summary>
public sealed class ChangeServiceLevelCommand(Booking booking, string newServiceLevel) : IBookingCommand
{
    private string? _priorServiceLevel;

    public void Execute()
    {
        _priorServiceLevel = booking.ServiceLevel;
        booking.ServiceLevel = newServiceLevel;
    }

    public void Undo()
    {
        if (_priorServiceLevel is null)
        {
            throw new InvalidOperationException("Undo was called before Execute.");
        }

        booking.ServiceLevel = _priorServiceLevel;
    }
}
