using System.Diagnostics.CodeAnalysis;

namespace RateEngineSeams.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: a sealed,
/// non-virtual rate engine with no seam for either of its collaborators. It
/// builds its own <see cref="LegacyTariffTable"/> with a hard-coded
/// <c>new</c> instead of receiving one, and reads the ambient wall clock
/// through <see cref="DateTime.Now"/> instead of an injected clock, so a test
/// can control neither the tariff rate nor the peak-hour surcharge without
/// waiting for the real clock to cross the boundary.
/// </summary>
public sealed class RateEngine
{
    /// <summary>
    /// Quotes a rate with no seam for the test to control: the tariff table
    /// is a hard-coded <c>new</c> and the peak-hour surcharge reads the
    /// ambient wall clock directly, both un-substitutable from outside this
    /// method.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part-13 before/after exception: this method is the deliberately untestable " +
            "legacy subject under contrast with the refactored RateEngine, not a static helper. Making " +
            "it static would still leave it unseamed, but the point of the example is the missing " +
            "constructor seam on an instance method, which is what the refactored form repairs.")]
    public decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tariff = new LegacyTariffTable();
        var subtotal = request.DeclaredValue * 0.015m;
        subtotal += request.WeightKilograms * tariff.RatePerKilogram("STD");

        if (DateTime.Now.Hour is >= 8 and < 18)
        {
            subtotal *= 1.1m;
        }

        return subtotal;
    }
}
