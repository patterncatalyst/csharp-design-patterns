namespace BookingRequests;

/// <summary>
/// One priced line on a booking request, for example a fuel surcharge or a
/// residential delivery fee.
/// </summary>
public sealed record LineItem(string Description, Money Amount, int Quantity);
