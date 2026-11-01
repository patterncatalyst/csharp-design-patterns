namespace ThreadSafeTariffs;

/// <summary>
/// Publishes a single <see cref="TariffTable"/>, built on first use by
/// whichever caller touches <see cref="Value"/> first, and shared by every
/// caller after that. Backed by <see cref="Lazy{T}"/> in
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> mode, so the
/// framework itself guarantees the underlying builder runs at most once even
/// under concurrent first use, and a build failure is cached and rethrown to
/// every caller rather than silently retried.
/// </summary>
public sealed class LazyTariffProvider(ITariffBuilder builder)
{
    private readonly Lazy<TariffTable> _lazy = new(builder.Build, LazyThreadSafetyMode.ExecutionAndPublication);

    public TariffTable Value => _lazy.Value;

    public bool IsValueCreated => _lazy.IsValueCreated;
}
