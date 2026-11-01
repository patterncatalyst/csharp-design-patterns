namespace ThreadSafeTariffs;

/// <summary>
/// An immutable set of per-kilogram tariff rates keyed by lane code. The
/// constructor copies the rates it is given, so the table's contents cannot
/// change out from under a caller holding a reference, and this type relies
/// on its default reference identity rather than value equality, which is
/// what lets the tests in this example prove "built exactly once" with
/// <c>Assert.Same</c>.
/// </summary>
public sealed class TariffTable
{
    private readonly Dictionary<string, decimal> _ratesByLane;

    public TariffTable(IReadOnlyDictionary<string, decimal> ratesByLane)
    {
        ArgumentNullException.ThrowIfNull(ratesByLane);
        _ratesByLane = new Dictionary<string, decimal>(ratesByLane, StringComparer.Ordinal);
    }

    /// <summary>
    /// Looks up the per-kilogram rate for the given lane code from this
    /// table's own copied rate data.
    /// </summary>
    public decimal RatePerKilogram(string laneCode)
    {
        ArgumentNullException.ThrowIfNull(laneCode);
        return _ratesByLane[laneCode];
    }
}
