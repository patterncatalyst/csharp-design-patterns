namespace FreightValueTypes;

/// <summary>
/// A shipment weight in kilograms. Unlike <see cref="Money"/>,
/// <c>default(Weight)</c> is itself a benign value (<c>Kilograms == 0m</c>
/// is a legitimate, formattable weight), so this type needs no
/// <c>Zero</c> sentinel.
/// </summary>
public readonly record struct Weight(decimal Kilograms)
{
    /// <summary>The weight in kilograms, validated non-negative at construction.</summary>
    public decimal Kilograms { get; init; } = Kilograms >= 0m
        ? Kilograms
        : throw new ArgumentOutOfRangeException(nameof(Kilograms), Kilograms, "Weight cannot be negative.");

    public override string ToString() => $"{Kilograms:0.00} kg";
}
