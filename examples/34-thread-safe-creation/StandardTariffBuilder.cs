namespace ThreadSafeTariffs;

/// <summary>
/// The production <see cref="ITariffBuilder"/>: seeds the standard and
/// express lane rates a shipping tariff table is published with.
/// </summary>
public sealed class StandardTariffBuilder : ITariffBuilder
{
    public TariffTable Build() => new(new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["STD"] = 2.40m,
        ["EXP"] = 3.60m,
    });
}
