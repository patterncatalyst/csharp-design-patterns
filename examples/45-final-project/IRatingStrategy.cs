namespace ShipmentWorkflow;

/// <summary>
/// Rates a <see cref="BookingRequest"/>. Selected per <see cref="ServiceLevel"/>
/// by <see cref="RatingStrategies.For"/> so <see cref="RatingStage"/> never
/// branches on the service level itself.
/// </summary>
public interface IRatingStrategy
{
    RateQuote Quote(BookingRequest request);
}
