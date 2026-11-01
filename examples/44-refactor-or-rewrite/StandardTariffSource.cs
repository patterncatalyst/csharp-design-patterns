namespace RateEngineReplacement;

/// <summary>
/// The production <see cref="ITariffSource"/>: lane rates reachable through an
/// interface a caller can pass to either replacement engine's constructor,
/// instead of having a tariff table built for it the way the legacy engine
/// does. <c>STD</c> seeds the same rate the legacy table seeds, so the two
/// forms can be compared over identical data; <c>EXP</c> and <c>INTL</c> give
/// the equivalence test more than one lane to exercise.
/// </summary>
public sealed class StandardTariffSource : ITariffSource
{
    private readonly Dictionary<string, decimal> _ratesByLaneCode = new(StringComparer.Ordinal)
    {
        ["STD"] = 2.40m,
        ["EXP"] = 3.75m,
        ["INTL"] = 5.10m,
    };

    public decimal RatePerKilogram(string laneCode)
    {
        ArgumentNullException.ThrowIfNull(laneCode);
        return _ratesByLaneCode[laneCode];
    }
}
