namespace BookingRequests;

/// <summary>
/// A request to book a shipment. <see cref="Origin"/>, <see cref="Destination"/>,
/// and <see cref="DeclaredValue"/> are required members: an object initializer
/// that omits any of them fails to compile (CS9035), closing a gap a
/// GoF-era Builder could only close by convention and a runtime check.
/// Everything else carries a sensible default, so a caller states only what
/// differs from it.
/// </summary>
public sealed record BookingRequest
{
    public required string Origin { get; init; }

    public required string Destination { get; init; }

    public required Money DeclaredValue { get; init; }

    public string ServiceLevel { get; init; } = "standard";

    public IReadOnlyList<LineItem> LineItems { get; init; } = [];

    public bool InsuranceRequested { get; init; }

    public string? SpecialInstructions { get; init; }

    public string? CustomerReference { get; init; }
}
