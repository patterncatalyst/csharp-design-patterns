namespace ShipmentWorkflow;

/// <summary>
/// Rates a booking at the base rate: the declared-value and per-kilogram
/// components of <see cref="RatingMath"/>, with no surcharge.
/// </summary>
public sealed class StandardRatingStrategy : IRatingStrategy
{
    public RateQuote Quote(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amount = request.DeclaredValue * RatingMath.DeclaredValueRate
            + request.WeightKilograms * RatingMath.PerKilogram;

        return new RateQuote(request.ConsignmentId, amount);
    }
}
