namespace RateEngineReplacement;

/// <summary>
/// The shared rate constants both replacement engines compute from: the
/// ad-valorem rate applied to a consignment's declared value, and the peak
/// surcharge multiplier added on top of the subtotal inside the peak window.
/// Kept in one place so the refactored engine and the rewritten engine read
/// the same numbers instead of each hard-coding its own copy.
/// </summary>
internal static class RateRules
{
    internal const decimal DeclaredValueRate = 0.015m;
    internal const decimal PeakMultiplier = 0.1m;
}
