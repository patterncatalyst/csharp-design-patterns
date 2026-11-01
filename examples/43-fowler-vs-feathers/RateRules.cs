namespace RateEngineCatalogues;

/// <summary>
/// The rate rules the legacy and Feathers-seamed engines carry as inline
/// literals, named here as the Fowler stage's remaining extraction: the
/// ad-valorem rate, the peak-hour multiplier, and the one lane this example
/// rates against.
/// </summary>
internal static class RateRules
{
    internal const decimal DeclaredValueRate = 0.015m;
    internal const decimal PeakMultiplier = 0.1m;
    internal const string StandardLane = "STD";
}
