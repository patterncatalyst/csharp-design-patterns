namespace TariffTables;

/// <summary>
/// Something that can load the full set of <see cref="TariffEntry"/> rows, at
/// a cost worth paying only once. A real implementation might call a rates
/// service or parse a large published tariff file; this example never needs
/// that I/O to prove the point, so <see cref="CountingTariffSource"/> stands
/// in for it.
/// </summary>
public interface ITariffSource
{
    /// <summary>Loads the full tariff table.</summary>
    IReadOnlyList<TariffEntry> Load();

    /// <summary>How many times <see cref="Load"/> has actually run.</summary>
    int LoadCount { get; }
}
