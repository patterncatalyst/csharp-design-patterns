namespace SailingBookings;

/// <summary>
/// Confirms a consignment against the booking ledger, returning the sequence
/// number under which it was recorded.
/// </summary>
public interface IBookingLedger
{
    Task<int> ConfirmAsync(string consignmentId, CancellationToken ct);
}
