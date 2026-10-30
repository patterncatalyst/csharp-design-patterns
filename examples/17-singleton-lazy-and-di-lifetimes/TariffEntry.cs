namespace TariffTables;

/// <summary>
/// One priced lane in a tariff table: the per-kilogram rate for shipping from
/// <see cref="Origin"/> to <see cref="Destination"/>.
/// </summary>
public sealed record TariffEntry(string Origin, string Destination, decimal RatePerKilogram);
