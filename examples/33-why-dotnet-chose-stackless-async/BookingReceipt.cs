namespace SailingBookings;

/// <summary>
/// Confirmation that a consignment was accepted, carrying the sequence number
/// assigned by the ledger that confirmed it.
/// </summary>
public sealed record BookingReceipt(string ConsignmentId, int LedgerSequence);
