namespace ConsignmentAmendments;

/// <summary>
/// A consignment weight in kilograms. A <c>readonly record struct</c> gives
/// value equality, a stable hash code, and a no-allocation copy on every
/// assignment from the declaration alone.
/// </summary>
public readonly record struct Weight(decimal Kilograms)
{
    /// <summary>The weight in kilograms, validated non-negative at construction.</summary>
    public decimal Kilograms { get; init; } = Kilograms >= 0m
        ? Kilograms
        : throw new ArgumentOutOfRangeException(nameof(Kilograms), Kilograms, "Weight cannot be negative.");

    public override string ToString() => $"{Kilograms:0.00} kg";
}
