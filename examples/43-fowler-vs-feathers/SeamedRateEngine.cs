namespace RateEngineCatalogues;

/// <summary>
/// The Feathers stage: the minimal seam that makes <see cref="Legacy.LegacyRateEngine"/>
/// testable. Only the two collaborators move to the constructor, an
/// <see cref="ITariffSource"/> and an <see cref="IClock"/>; the loose
/// <c>(decimal, decimal, string)</c> signature, the inline string
/// <c>switch</c> for the service surcharge, and the inline rate literals all
/// stay exactly as the legacy shape had them. This is "the seam, not the
/// redesign": the smallest change that lets a test substitute both
/// collaborators without touching the method's structure.
/// </summary>
public sealed class SeamedRateEngine(ITariffSource tariffs, IClock clock)
{
    /// <summary>
    /// Quotes a rate using the injected <see cref="ITariffSource"/> and
    /// <see cref="IClock"/> in place of the legacy shape's hard-coded
    /// <c>new LegacyTariffTable()</c> and ambient <see cref="DateTime.Now"/>,
    /// with the rest of the method's structure, including the inline
    /// <c>switch</c> and rate literals, left untouched.
    /// </summary>
    public decimal Quote(decimal declaredValue, decimal weightKilograms, string serviceLevel)
    {
        var subtotal = declaredValue * 0.015m;
        subtotal += weightKilograms * tariffs.RatePerKilogram("STD");

        switch (serviceLevel)
        {
            case "EXPRESS":
                subtotal *= 1.25m;
                break;
            case "OVERNIGHT":
                subtotal *= 1.5m;
                break;
        }

        if (PeakWindow.IsPeak(clock.UtcNow))
        {
            subtotal *= 1.1m;
        }

        return subtotal;
    }
}
