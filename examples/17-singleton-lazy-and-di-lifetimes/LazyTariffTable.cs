namespace TariffTables;

/// <summary>
/// Tier 2: Singleton as a construction-time guarantee instead of a type-level
/// one. <see cref="LazyTariffTable"/> is an ordinary object (nothing stops a
/// caller from constructing two of them), but each instance wraps its
/// <see cref="TariffTable"/> in a <see cref="Lazy{T}"/> built with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so the
/// expensive <see cref="ITariffSource.Load"/> call behind it runs at most
/// once per instance, deferred until the first read, and safely shared across
/// however many threads reach <see cref="Table"/> concurrently.
/// </summary>
public sealed class LazyTariffTable
{
    private readonly Lazy<TariffTable> _lazyTable;

    public LazyTariffTable(ITariffSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _lazyTable = new Lazy<TariffTable>(() => new TariffTable(source), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The underlying <see cref="TariffTable"/>. The first read on any thread
    /// triggers the load; every read after that, on any thread, returns the
    /// same already-built instance.
    /// </summary>
    public TariffTable Table => _lazyTable.Value;

    /// <summary>Convenience pass-through so callers rarely need <see cref="Table"/> directly.</summary>
    public decimal Lookup(string origin, string destination) => Table.Lookup(origin, destination);
}
