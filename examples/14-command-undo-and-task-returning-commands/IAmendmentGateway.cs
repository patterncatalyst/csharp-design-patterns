namespace BookingAmendments;

/// <summary>
/// The remote booking system an async command confirms or reverts an
/// amendment against. A real implementation would call a carrier's booking
/// API; this example only ever substitutes it.
/// </summary>
public interface IAmendmentGateway
{
    Task ConfirmAsync(string bookingId, CancellationToken cancellationToken);

    Task RevertAsync(string bookingId, CancellationToken cancellationToken);
}
