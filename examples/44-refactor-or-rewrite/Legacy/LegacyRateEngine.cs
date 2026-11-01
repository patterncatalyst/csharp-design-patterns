using System.Diagnostics.CodeAnalysis;

namespace RateEngineReplacement.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: a sealed,
/// non-virtual rate engine with no stable seam to refactor or rewrite behind.
/// It builds its own <see cref="LegacyTariffTable"/> with a hard-coded
/// <c>new</c> instead of receiving one, reads the ambient wall clock through
/// <see cref="DateTime.Now"/> instead of an injected clock, and does not
/// implement <see cref="IRateEngine"/>, so neither replacement engine, nor a
/// caller bound to the seam, can be swapped in for it without a source
/// change at every call site.
/// </summary>
public sealed class LegacyRateEngine
{
    /// <summary>
    /// Quotes a rate with no seam for a caller to intercept: the tariff table
    /// is a hard-coded <c>new</c> and the peak-hour surcharge reads the
    /// ambient wall clock directly, both un-substitutable from outside this
    /// method, and the method itself is reachable only by its own concrete
    /// type.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part-13 before/after exception: this method is the deliberately untestable " +
            "legacy subject under contrast with the refactored and rewritten engines, not a static " +
            "helper. Making it static would still leave it without a stable seam, but the point of the " +
            "example is the missing IRateEngine seam on an instance method, which both replacements " +
            "repair.")]
    public decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tariff = new LegacyTariffTable();
        var subtotal = request.DeclaredValue * 0.015m;
        subtotal += request.WeightKilograms * tariff.RatePerKilogram(request.LaneCode);

        if (DateTime.Now.Hour is >= 8 and < 18)
        {
            subtotal *= 1.1m;
        }

        return subtotal;
    }
}
