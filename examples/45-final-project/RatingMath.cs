namespace ShipmentWorkflow;

/// <summary>
/// The shared rate constants both rating strategies compute from: the
/// ad-valorem rate applied to a consignment's declared value, the
/// per-kilogram rate applied to its weight, and the multiplier
/// <see cref="ExpressRatingStrategy"/> applies on top of the standard base
/// amount. Kept in one place so both strategies read the same numbers instead
/// of each hard-coding its own copy.
/// </summary>
internal static class RatingMath
{
    internal const decimal DeclaredValueRate = 0.015m;
    internal const decimal PerKilogram = 2.50m;
    internal const decimal ExpressMultiplier = 1.40m;
}
