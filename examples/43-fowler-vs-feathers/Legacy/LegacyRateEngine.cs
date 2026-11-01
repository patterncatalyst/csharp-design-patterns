using System.Diagnostics.CodeAnalysis;

namespace RateEngineCatalogues.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: a sealed,
/// non-virtual rate engine with no seam for either collaborator and no
/// extracted service-surcharge strategy. It builds its own
/// <see cref="LegacyTariffTable"/> with a hard-coded <c>new</c> instead of
/// receiving one, reads the ambient wall clock through
/// <see cref="DateTime.Now"/> instead of an injected clock, and selects the
/// service surcharge with an inline string <c>switch</c> instead of a
/// polymorphic strategy, so a test can control none of it without waiting for
/// the real clock to cross the peak-hour boundary.
/// </summary>
public sealed class LegacyRateEngine
{
    /// <summary>
    /// Quotes a rate with no seam for the test to control: the tariff table
    /// is a hard-coded <c>new</c>, the service surcharge is an inline string
    /// <c>switch</c>, and the peak-hour surcharge reads the ambient wall
    /// clock directly, all un-substitutable from outside this method.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part-13 before/after exception: this method is the deliberately untestable " +
            "legacy subject under contrast with SeamedRateEngine and RateEngine, not a static helper. " +
            "Making it static would still leave it unseamed, but the point of the example is the " +
            "missing constructor seam on an instance method, which the refactored forms repair.")]
    public decimal Quote(decimal declaredValue, decimal weightKilograms, string serviceLevel)
    {
        var tariff = new LegacyTariffTable();
        var subtotal = declaredValue * 0.015m;
        subtotal += weightKilograms * tariff.RatePerKilogram("STD");

        switch (serviceLevel)
        {
            case "EXPRESS":
                subtotal *= 1.25m;
                break;
            case "OVERNIGHT":
                subtotal *= 1.5m;
                break;
        }

        if (DateTime.Now.Hour is >= 8 and < 18)
        {
            subtotal *= 1.1m;
        }

        return subtotal;
    }
}
