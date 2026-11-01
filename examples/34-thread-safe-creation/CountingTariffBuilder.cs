namespace ThreadSafeTariffs;

/// <summary>
/// An <see cref="ITariffBuilder"/> decorator that counts how many times
/// <see cref="Build"/> actually ran, wrapping another builder such as
/// <see cref="StandardTariffBuilder"/>. The counter is incremented with
/// <see cref="Interlocked"/> so the count itself stays correct even when
/// many threads race to call <see cref="Build"/> at once, which is what lets
/// the concurrency tests in this example assert "built exactly once" is true
/// even if every caller happens to land on the same thread.
/// </summary>
public sealed class CountingTariffBuilder(ITariffBuilder inner) : ITariffBuilder
{
    private int _buildCount;

    public int BuildCount => Volatile.Read(ref _buildCount);

    public TariffTable Build()
    {
        Interlocked.Increment(ref _buildCount);
        return inner.Build();
    }
}
