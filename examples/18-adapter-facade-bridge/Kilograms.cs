namespace CarrierIntegration;

/// <summary>
/// A weight expressed in kilograms, the unit every carrier gateway in this
/// example speaks on its <see cref="ShipmentRequest"/> side. Converting to
/// <see cref="Pounds"/> is explicit: the conversion factor is not exact in
/// decimal, so the result is a rounded approximation rather than a lossless
/// restatement of the same quantity.
/// </summary>
public readonly record struct Kilograms(decimal Value)
{
    private const decimal PoundsPerKilogram = 2.20462m;

    /// <summary>Converts this weight to pounds using the standard factor.</summary>
    public Pounds ToPounds() => new(Value * PoundsPerKilogram);

    /// <summary>
    /// Converts a kilogram weight to pounds. Explicit because the factor is
    /// an approximation: the result carries rounding, not the same value in
    /// a different unit.
    /// </summary>
    public static explicit operator Pounds(Kilograms kg) => kg.ToPounds();
}
