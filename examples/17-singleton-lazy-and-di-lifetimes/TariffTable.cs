namespace TariffTables;

/// <summary>
/// An already-loaded, queryable tariff table. Construction is the expensive
/// step this whole example exists to guard: the constructor calls
/// <see cref="ITariffSource.Load"/> exactly once and indexes the result, so
/// every <see cref="Lookup"/> afterward is a plain dictionary read.
/// </summary>
public sealed class TariffTable
{
    private readonly Dictionary<(string Origin, string Destination), decimal> _ratesByLane;

    public TariffTable(ITariffSource source)
        : this((source ?? throw new ArgumentNullException(nameof(source))).Load())
    {
    }

    public TariffTable(IReadOnlyList<TariffEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _ratesByLane = entries.ToDictionary(entry => (entry.Origin, entry.Destination), entry => entry.RatePerKilogram);
    }

    /// <summary>
    /// The per-kilogram rate from <paramref name="origin"/> to
    /// <paramref name="destination"/>. Throws if the lane was never published
    /// in the loaded tariff set, rather than silently returning zero.
    /// </summary>
    public decimal Lookup(string origin, string destination) =>
        _ratesByLane.TryGetValue((origin, destination), out var ratePerKilogram)
            ? ratePerKilogram
            : throw new KeyNotFoundException($"No published tariff from '{origin}' to '{destination}'.");
}
