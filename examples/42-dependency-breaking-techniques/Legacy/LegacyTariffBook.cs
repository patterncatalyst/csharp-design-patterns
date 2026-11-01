namespace RateEngineDependencies.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: a per-kilogram
/// lane rate reached only through a static method, so no test can substitute
/// a different tariff without a static mock framework.
/// </summary>
public static class LegacyTariffBook
{
    /// <summary>
    /// Looks up the per-kilogram rate for a lane code. Seeded with the same
    /// rates as <see cref="StandardTariffBook"/> so the legacy and refactored
    /// forms quote identically over the same request.
    /// </summary>
    public static decimal RateFor(string laneCode) => laneCode switch
    {
        "ROT-NYC" => 4.25m,
        "SIN-LAX" => 5.10m,
        "HKG-FRA" => 6.75m,
        _ => 2.00m,
    };
}
