namespace RateEngineDependencies;

/// <summary>
/// The production <see cref="ITariffBook"/>. Seeded with the same rates as
/// <see cref="Legacy.LegacyTariffBook"/> so the legacy and refactored engines
/// quote identically over the same request.
/// </summary>
public sealed class StandardTariffBook : ITariffBook
{
    public decimal RateFor(string laneCode) => laneCode switch
    {
        "ROT-NYC" => 4.25m,
        "SIN-LAX" => 5.10m,
        "HKG-FRA" => 6.75m,
        _ => 2.00m,
    };
}
