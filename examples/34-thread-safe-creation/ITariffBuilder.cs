namespace ThreadSafeTariffs;

/// <summary>
/// Builds a <see cref="TariffTable"/>. The seam every provider in this
/// example builds against: a lazily-published singleton, a lock-guarded
/// singleton, and a test double that counts calls or throws, all implement
/// this one interface.
/// </summary>
public interface ITariffBuilder
{
    TariffTable Build();
}
