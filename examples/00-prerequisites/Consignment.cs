namespace Consignments;

/// <summary>
/// Identifies a consignment booked with a carrier. Wraps a non-empty booking
/// reference so an empty or whitespace id can never reach the rest of the
/// domain.
/// </summary>
public readonly record struct ConsignmentId(string Value)
{
    public string Value { get; init; } = string.IsNullOrWhiteSpace(Value)
        ? throw new ArgumentException("Consignment id cannot be empty.", nameof(Value))
        : Value;

    public override string ToString() => Value;
}

/// <summary>
/// A physical weight in kilograms. Carriers reject a zero or negative weight
/// at the gate, so the type refuses to represent one.
/// </summary>
public readonly record struct Weight(decimal Kilograms)
{
    public decimal Kilograms { get; init; } = Kilograms > 0
        ? Kilograms
        : throw new ArgumentOutOfRangeException(
            nameof(Kilograms), Kilograms, "Weight must be a positive number of kilograms.");

    public override string ToString() => $"{Kilograms:0.##} kg";
}

/// <summary>
/// A single shipment booked for carriage to a destination. The primary
/// constructor validates <see cref="Destination"/> by redeclaring the
/// property with an initializer; <see cref="ConsignmentId"/> and
/// <see cref="Weight"/> validate themselves the same way, so a
/// <see cref="Consignment"/> can never hold an empty id, a non-positive
/// weight, or a blank destination.
/// </summary>
public sealed record Consignment(ConsignmentId Id, string Destination, Weight Weight)
{
    public string Destination { get; init; } = string.IsNullOrWhiteSpace(Destination)
        ? throw new ArgumentException("Destination cannot be empty.", nameof(Destination))
        : Destination;
}
