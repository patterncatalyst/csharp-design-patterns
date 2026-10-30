namespace BookingRequests;

/// <summary>
/// Prototype instances reused across repeat bookings for the same shipping
/// lane. Cloning and amending an existing instance with <c>with</c> replaces
/// GoF's <c>Clone</c> method: the language performs the copy, and the
/// <c>with</c> initializer states only the fields that differ from the
/// prototype.
/// </summary>
public static class BookingTemplates
{
    /// <summary>
    /// A fully populated domestic booking between two recurring warehouse
    /// hubs: standard service, insured, one fuel-surcharge line item, and a
    /// standing customer reference.
    /// </summary>
    public static BookingRequest StandardDomestic { get; } = new()
    {
        Origin = "Columbus, OH",
        Destination = "Memphis, TN",
        DeclaredValue = Money.Of(1200.00m),
        ServiceLevel = "standard",
        LineItems = [new LineItem("Fuel surcharge", Money.Of(45.00m), 1)],
        InsuranceRequested = true,
        SpecialInstructions = "Dock door 4, weekday deliveries only.",
        CustomerReference = "WH-COL-MEM",
    };
}
