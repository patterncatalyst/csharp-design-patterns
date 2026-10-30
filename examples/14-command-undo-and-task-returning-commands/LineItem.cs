namespace BookingAmendments;

/// <summary>
/// A single billable line on a <see cref="Booking"/>, for example a surcharge
/// or a rebooking fee added by an amendment command.
/// </summary>
public sealed record LineItem(string Description, Money Amount);
