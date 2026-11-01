namespace RateEngineCatalogues;

/// <summary>
/// The production <see cref="ITariffSource"/>: the same lane rate the legacy
/// table seeds, now reachable through an interface a caller can pass to a
/// constructor instead of having it built for them.
/// </summary>
public sealed class StandardTariffSource : ITariffSource
{
    private readonly Dictionary<string, decimal> _ratesByLaneCode = new(StringComparer.Ordinal)
    {
        ["STD"] = 2.40m,
    };

    public decimal RatePerKilogram(string laneCode)
    {
        ArgumentNullException.ThrowIfNull(laneCode);
        return _ratesByLaneCode[laneCode];
    }
}
