namespace ShipmentWorkflow;

/// <summary>
/// Rates a booking at the express rate: the same standard base amount as
/// <see cref="StandardRatingStrategy"/>, multiplied by
/// <see cref="RatingMath.ExpressMultiplier"/>.
/// </summary>
public sealed class ExpressRatingStrategy : IRatingStrategy
{
    public RateQuote Quote(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseAmount = request.DeclaredValue * RatingMath.DeclaredValueRate
            + request.WeightKilograms * RatingMath.PerKilogram;

        return new RateQuote(request.ConsignmentId, baseAmount * RatingMath.ExpressMultiplier);
    }
}
