namespace ThreadSafeTariffs.Tests;

/// <summary>
/// An <see cref="ITariffBuilder"/> test double that always fails, used to
/// prove <see cref="LazyTariffProvider"/> caches a build failure instead of
/// silently retrying it on the next access.
/// </summary>
public sealed class ThrowingTariffBuilder : ITariffBuilder
{
    public TariffTable Build() => throw new InvalidOperationException("Tariff feed unavailable.");
}
