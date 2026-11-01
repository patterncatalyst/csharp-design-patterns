namespace RateEngineSeams.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: a tariff table
/// that seeds its own rates in its constructor, so a caller can never supply
/// a different rate for a test without touching the real lane data.
/// </summary>
public sealed class LegacyTariffTable
{
    private readonly Dictionary<string, decimal> _ratesByLaneCode;

    public LegacyTariffTable()
    {
        _ratesByLaneCode = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["STD"] = 2.40m,
        };
    }

    /// <summary>
    /// Looks up the per-kilogram rate for the given lane code from this
    /// table's own seeded instance state.
    /// </summary>
    public decimal RatePerKilogram(string laneCode)
    {
        ArgumentNullException.ThrowIfNull(laneCode);
        return _ratesByLaneCode[laneCode];
    }
}
