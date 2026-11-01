namespace ShipmentWorkflow;

/// <summary>
/// Selects the <see cref="IRatingStrategy"/> for a <see cref="ServiceLevel"/>,
/// so callers never construct a strategy directly.
/// </summary>
public static class RatingStrategies
{
    public static IRatingStrategy For(ServiceLevel service) => service switch
    {
        ServiceLevel.Standard => new StandardRatingStrategy(),
        ServiceLevel.Express => new ExpressRatingStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service level."),
    };
}
