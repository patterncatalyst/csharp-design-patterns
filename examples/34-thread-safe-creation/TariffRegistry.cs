namespace ThreadSafeTariffs;

/// <summary>
/// The simplest thread-safe creation strategy of the three in this example:
/// a static field initializer. The runtime guarantees a type's static field
/// initializers run at most once, before the first access to any of its
/// static members, so <see cref="Instance"/> is built eagerly, exactly once,
/// with no lock and no <see cref="Lazy{T}"/> wrapper at all.
/// </summary>
public static class TariffRegistry
{
    private static readonly TariffTable _instance = new StandardTariffBuilder().Build();

    public static TariffTable Instance => _instance;
}
