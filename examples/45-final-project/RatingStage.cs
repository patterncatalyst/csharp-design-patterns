namespace ShipmentWorkflow;

/// <summary>
/// Rates the booking by delegating to the <see cref="IRatingStrategy"/>
/// <see cref="RatingStrategies.For"/> selects for its
/// <see cref="ServiceLevel"/>, and records the result on the context.
/// </summary>
public sealed class RatingStage : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var quote = RatingStrategies.For(context.Booking.Service).Quote(context.Booking);
        return context with { Quote = quote };
    }
}
