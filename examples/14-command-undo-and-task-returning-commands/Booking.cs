namespace BookingAmendments;

/// <summary>
/// The receiver every command in this example mutates. <see cref="ServiceLevel"/>
/// and <see cref="WeightKilograms"/> are plain settable properties, and
/// <see cref="LineItems"/> exposes a read-only view over a private mutable
/// list, so a command can change state directly and a test can observe the
/// result without any command-specific accessor.
/// </summary>
public sealed class Booking(string bookingId, string serviceLevel, decimal weightKilograms)
{
    private readonly List<LineItem> _lineItems = [];

    public string BookingId { get; } = bookingId;

    public string ServiceLevel { get; set; } = serviceLevel;

    public decimal WeightKilograms { get; set; } = weightKilograms;

    public IReadOnlyList<LineItem> LineItems => _lineItems;

    public void AddLineItem(LineItem lineItem) => _lineItems.Add(lineItem);

    public void RemoveLineItem(LineItem lineItem) => _lineItems.Remove(lineItem);
}
