namespace ThreadSafeTariffs;

/// <summary>
/// Publishes a single <see cref="TariffTable"/>, built on first use and
/// shared by every caller after that, the same contract as
/// <see cref="LazyTariffProvider"/> but written out by hand with the modern
/// <see cref="System.Threading.Lock"/> type instead of <see cref="Lazy{T}"/>.
/// The gate is held only long enough to check and, on first use, build and
/// cache the table; this is a purely synchronous creation path, so there is
/// no <c>await</c> inside the lock body.
/// </summary>
public sealed class LockTariffProvider(ITariffBuilder builder)
{
    private readonly Lock _gate = new();
    private TariffTable? _table;

    public TariffTable Value
    {
        get
        {
            lock (_gate)
            {
                return _table ??= builder.Build();
            }
        }
    }
}
