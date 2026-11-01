namespace RatingReview;

/// <summary>
/// Prices a <see cref="BookingRequest"/> into a carrier quote. Two concrete
/// strategies price the same request differently, so the abstraction earns
/// its place: <c>SingleImplementationStrategyAnalyzer</c> has nothing to
/// flag here. This is the corrected, reviewed form of a sample that an LLM
/// first drafted with only one strategy in place.
/// </summary>
public interface IRatingStrategy
{
    decimal Quote(BookingRequest request);
}

/// <summary>
/// Standard ground service: a flat per-kilogram rate plus a declared-value
/// insurance surcharge.
/// </summary>
public sealed class StandardRatingStrategy : IRatingStrategy
{
    private const decimal PerKilogramRate = 2.35m;
    private const decimal InsuranceRate = 0.004m;

    public decimal Quote(BookingRequest request) =>
        request.WeightKilograms * PerKilogramRate + request.DeclaredValue * InsuranceRate;
}

/// <summary>
/// Express air service: a higher base rate per kilogram, a fixed handling
/// fee, and a steeper insurance surcharge for the faster transit.
/// </summary>
public sealed class ExpressRatingStrategy : IRatingStrategy
{
    private const decimal PerKilogramRate = 6.10m;
    private const decimal HandlingFee = 18.00m;
    private const decimal InsuranceRate = 0.009m;

    public decimal Quote(BookingRequest request) =>
        request.WeightKilograms * PerKilogramRate + HandlingFee + request.DeclaredValue * InsuranceRate;
}
