namespace RateEngineCatalogues;

/// <summary>
/// Maps a <see cref="ServiceLevel"/> to its <see cref="IServiceSurcharge"/>
/// strategy, the Fowler-stage replacement for the inline string
/// <c>switch</c> both the legacy and the Feathers-seamed engines carry.
/// </summary>
public static class ServiceSurcharges
{
    public static IServiceSurcharge For(ServiceLevel level) => level switch
    {
        ServiceLevel.Standard => new StandardSurcharge(),
        ServiceLevel.Express => new ExpressSurcharge(),
        ServiceLevel.Overnight => new OvernightSurcharge(),
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown service level."),
    };
}
