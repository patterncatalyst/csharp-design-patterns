namespace SailingBookings;

/// <summary>
/// A single consignment's request to be accepted onto a sailing.
/// </summary>
public readonly record struct BookingRequest(string ConsignmentId);
