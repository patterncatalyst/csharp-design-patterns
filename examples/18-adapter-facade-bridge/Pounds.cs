namespace CarrierIntegration;

/// <summary>
/// A weight expressed in pounds, the unit the Airco legacy client speaks.
/// Converting back to <see cref="Kilograms"/> is explicit for the same
/// reason the forward conversion is: the factor is not exact in decimal, so
/// a round trip is an approximation, not an identity.
/// </summary>
public readonly record struct Pounds(decimal Value)
{
    private const decimal PoundsPerKilogram = 2.20462m;

    /// <summary>Converts this weight to kilograms using the standard factor.</summary>
    public Kilograms ToKilograms() => new(Value / PoundsPerKilogram);

    /// <summary>
    /// Converts a pound weight to kilograms. Explicit because the factor is
    /// an approximation: the result carries rounding, not the same value in
    /// a different unit.
    /// </summary>
    public static explicit operator Kilograms(Pounds lb) => lb.ToKilograms();
}
