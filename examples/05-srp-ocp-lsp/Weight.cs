namespace RateCalculation;

/// <summary>
/// A physical shipment weight in kilograms. Carriers reject a zero or
/// negative weight at the gate, so the type refuses to represent one.
/// </summary>
public readonly record struct Weight(decimal Kilograms)
{
    public decimal Kilograms { get; init; } = Kilograms > 0
        ? Kilograms
        : throw new ArgumentOutOfRangeException(
            nameof(Kilograms), Kilograms, "Weight must be a positive number of kilograms.");

    public override string ToString() => $"{Kilograms:0.##} kg";
}
