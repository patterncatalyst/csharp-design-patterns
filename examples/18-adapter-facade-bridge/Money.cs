namespace CarrierIntegration;

/// <summary>
/// A monetary amount stored as an integral cent count, so arithmetic never
/// drifts the way repeated decimal addition can. The conversions to and from
/// <see cref="decimal"/> dollars are opposite in kind: widening cents to
/// dollars never loses information, so it is implicit, while narrowing
/// dollars back to cents can discard sub-cent precision, so it is explicit.
/// Each operator has a named alternate (<see cref="ToDecimal"/> and
/// <see cref="FromDecimal"/>) for callers who prefer a method to an operator,
/// and so the conversion is discoverable without already knowing the type
/// supports it.
/// </summary>
public readonly record struct Money(long Cents)
{
    /// <summary>Converts the stored cents to dollars. Always exact.</summary>
    public decimal ToDecimal() => Cents / 100m;

    /// <summary>
    /// Widens a <see cref="Money"/> value to <see cref="decimal"/> dollars.
    /// Implicit because every cent count converts to dollars exactly.
    /// </summary>
    public static implicit operator decimal(Money money) => money.ToDecimal();

    /// <summary>
    /// Converts dollars to the nearest whole cent, rounding to even on a
    /// tie. A caller that needs cent-exact money can call this directly
    /// instead of relying on the cast below.
    /// </summary>
    public static Money FromDecimal(decimal dollars) =>
        new(checked((long)Math.Round(dollars * 100m, MidpointRounding.ToEven)));

    /// <summary>
    /// Narrows dollars to a <see cref="Money"/> value. Explicit because a
    /// fractional-cent amount, for example 1.005m, is rounded rather than
    /// preserved.
    /// </summary>
    public static explicit operator Money(decimal dollars) => FromDecimal(dollars);
}
