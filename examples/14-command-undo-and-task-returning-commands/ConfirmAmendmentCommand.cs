namespace BookingAmendments;

/// <summary>
/// Confirms a booking amendment against <see cref="IAmendmentGateway"/>, and
/// reverts the same booking if the command is later undone. Every await is
/// configured not to resume on the captured context, and the caller's
/// cancellation token is threaded through rather than left implicit, since
/// this is library code with no UI thread to return to.
/// </summary>
public sealed class ConfirmAmendmentCommand(IAmendmentGateway gateway, string bookingId) : IAsyncBookingCommand
{
    public async Task ExecuteAsync(CancellationToken cancellationToken) =>
        await gateway.ConfirmAsync(bookingId, cancellationToken).ConfigureAwait(false);

    public async Task UndoAsync(CancellationToken cancellationToken) =>
        await gateway.RevertAsync(bookingId, cancellationToken).ConfigureAwait(false);
}
