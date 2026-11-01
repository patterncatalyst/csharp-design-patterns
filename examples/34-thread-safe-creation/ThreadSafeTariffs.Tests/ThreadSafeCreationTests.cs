using Xunit;

namespace ThreadSafeTariffs.Tests;

/// <summary>
/// Proves the three thread-safe creation strategies in this example really
/// do build their shared <see cref="TariffTable"/> exactly once, even when
/// many callers race for it at the same time. Each concurrency test uses a
/// <see cref="Barrier"/> to release a fixed number of <see cref="Task.Run"/>
/// readers as close to simultaneously as the scheduler allows, maximizing the
/// chance of real contention on first use. The assertions themselves, built
/// exactly once and every caller got the same reference, hold regardless of
/// whether the race actually manifests on a given run, which is what keeps
/// this example deterministic.
/// </summary>
public class ThreadSafeCreationTests
{
    private const int ConcurrentReaders = 16;

    [Fact]
    public async Task Lazy_provider_builds_the_tariff_table_exactly_once_under_concurrent_first_use()
    {
        var counting = new CountingTariffBuilder(new StandardTariffBuilder());
        var provider = new LazyTariffProvider(counting);

        var results = await ReadConcurrentlyAsync(() => provider.Value);

        Assert.Equal(1, counting.BuildCount);
        AssertAllSameInstance(results);
    }

    [Fact]
    public void Lazy_provider_does_not_build_until_the_value_is_touched()
    {
        var counting = new CountingTariffBuilder(new StandardTariffBuilder());
        var provider = new LazyTariffProvider(counting);

        Assert.False(provider.IsValueCreated);
        Assert.Equal(0, counting.BuildCount);

        _ = provider.Value;

        Assert.True(provider.IsValueCreated);
        Assert.Equal(1, counting.BuildCount);
    }

    [Fact]
    public void Lazy_provider_caches_a_build_failure_and_rethrows_it()
    {
        var provider = new LazyTariffProvider(new ThrowingTariffBuilder());

        var firstFailure = Assert.Throws<InvalidOperationException>(() => provider.Value);
        var secondFailure = Assert.Throws<InvalidOperationException>(() => provider.Value);

        Assert.Same(firstFailure, secondFailure);
    }

    [Fact]
    public async Task Lock_provider_builds_the_tariff_table_exactly_once_under_concurrent_first_use()
    {
        var counting = new CountingTariffBuilder(new StandardTariffBuilder());
        var provider = new LockTariffProvider(counting);

        var results = await ReadConcurrentlyAsync(() => provider.Value);

        Assert.Equal(1, counting.BuildCount);
        AssertAllSameInstance(results);
    }

    [Fact]
    public async Task Static_singleton_hands_every_caller_the_same_instance()
    {
        var results = await ReadConcurrentlyAsync(() => TariffRegistry.Instance);

        AssertAllSameInstance(results);
    }

    /// <summary>
    /// Releases <see cref="ConcurrentReaders"/> tasks through a
    /// <see cref="Barrier"/> so they all call <paramref name="read"/> as
    /// close to simultaneously as the scheduler allows, then joins every
    /// result with <see cref="Task.WhenAll{TResult}(Task{TResult}[])"/>.
    /// </summary>
    private static async Task<TariffTable[]> ReadConcurrentlyAsync(Func<TariffTable> read)
    {
        using var barrier = new Barrier(ConcurrentReaders);

        var tasks = new Task<TariffTable>[ConcurrentReaders];
        for (var i = 0; i < ConcurrentReaders; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                return read();
            });
        }

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Asserts every result in <paramref name="results"/> is the identical
    /// reference as the first, the reference-identity check that proves a
    /// single shared <see cref="TariffTable"/> was published to every caller.
    /// </summary>
    private static void AssertAllSameInstance(TariffTable[] results)
    {
        Assert.NotEmpty(results);
        var first = results[0];
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Same(first, result);
        });
    }
}
