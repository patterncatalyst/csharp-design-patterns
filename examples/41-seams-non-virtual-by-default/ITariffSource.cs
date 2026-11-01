namespace RateEngineSeams;

/// <summary>
/// The seam for a rate engine's tariff lookup: an interface a caller can
/// substitute in a test, instead of the legacy shape's hard-coded
/// <c>new LegacyTariffTable()</c>.
/// </summary>
public interface ITariffSource
{
    decimal RatePerKilogram(string laneCode);
}
