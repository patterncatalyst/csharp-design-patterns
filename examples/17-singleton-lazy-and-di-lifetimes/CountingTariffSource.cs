namespace TariffTables;

/// <summary>
/// An <see cref="ITariffSource"/> that counts every call to <see cref="Load"/>.
/// Standing in for an expensive rates-service call or a large tariff file
/// parse, it lets every test in this example prove single-init deterministically:
/// if a caching wrapper or DI lifetime is holding up its end, <see cref="LoadCount"/>
/// never exceeds one no matter how many times the table is read.
/// </summary>
public sealed class CountingTariffSource : ITariffSource
{
    private static readonly IReadOnlyList<TariffEntry> FixedEntries =
    [
        new TariffEntry("Rotterdam", "Santos", 4.00m),
        new TariffEntry("Rotterdam", "Shanghai", 6.50m),
        new TariffEntry("Santos", "Shanghai", 5.25m),
    ];

    private int _loadCount;

    public int LoadCount => _loadCount;

    public IReadOnlyList<TariffEntry> Load()
    {
        Interlocked.Increment(ref _loadCount);
        return FixedEntries;
    }
}
