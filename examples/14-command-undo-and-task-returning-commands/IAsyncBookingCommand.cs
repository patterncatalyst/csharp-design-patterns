namespace BookingAmendments;

/// <summary>
/// The Task-returning counterpart to <see cref="IBookingCommand"/>: an
/// amendment whose execute and undo steps are I/O-bound calls against a
/// remote gateway rather than in-memory field assignments.
/// </summary>
public interface IAsyncBookingCommand
{
    Task ExecuteAsync(CancellationToken cancellationToken);

    Task UndoAsync(CancellationToken cancellationToken);
}
