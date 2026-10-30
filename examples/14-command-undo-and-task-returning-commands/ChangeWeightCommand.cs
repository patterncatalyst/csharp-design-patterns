namespace BookingAmendments;

/// <summary>
/// Changes a booking's declared weight, snapshotting the prior value so
/// <see cref="Undo"/> restores it exactly, even after later commands have
/// moved the weight further.
/// </summary>
public sealed class ChangeWeightCommand(Booking booking, decimal newWeightKilograms) : IBookingCommand
{
    private decimal? _priorWeightKilograms;

    public void Execute()
    {
        _priorWeightKilograms = booking.WeightKilograms;
        booking.WeightKilograms = newWeightKilograms;
    }

    public void Undo()
    {
        if (_priorWeightKilograms is null)
        {
            throw new InvalidOperationException("Undo was called before Execute.");
        }

        booking.WeightKilograms = _priorWeightKilograms.Value;
    }
}
