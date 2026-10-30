using Xunit;

namespace TariffTables.Tests;

/// <summary>
/// Proves the Tier 2 shape: <see cref="LazyTariffTable"/> defers loading
/// until <see cref="LazyTariffTable.Table"/> is first read, loads at most
/// once no matter how many times it is read afterward, and keeps that
/// guarantee under concurrent access because it builds its
/// <see cref="Lazy{T}"/> with <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>.
/// </summary>
public class LazyTariffTableTests
{
    [Fact]
    public void Does_not_load_the_source_until_the_table_is_first_accessed()
    {
        var source = new CountingTariffSource();
        var lazyTable = new LazyTariffTable(source);

        Assert.Equal(0, source.LoadCount);

        _ = lazyTable.Table;

        Assert.Equal(1, source.LoadCount);
    }

    [Fact]
    public void Repeated_access_loads_the_source_exactly_once()
    {
        var source = new CountingTariffSource();
        var lazyTable = new LazyTariffTable(source);

        for (var i = 0; i < 5; i++)
        {
            _ = lazyTable.Table;
        }

        Assert.Equal(1, source.LoadCount);
    }

    [Fact]
    public void Concurrent_access_from_many_threads_still_loads_exactly_once()
    {
        var source = new CountingTariffSource();
        var lazyTable = new LazyTariffTable(source);

        Parallel.For(0, 64, index => _ = lazyTable.Table);

        Assert.Equal(1, source.LoadCount);
    }

    [Fact]
    public void Looks_up_the_published_rate_once_loaded()
    {
        var lazyTable = new LazyTariffTable(new CountingTariffSource());

        var ratePerKilogram = lazyTable.Lookup("Santos", "Shanghai");

        Assert.Equal(5.25m, ratePerKilogram);
    }
}
