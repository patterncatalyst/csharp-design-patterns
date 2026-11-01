namespace ShipmentWorkflow;

/// <summary>
/// Builds the final <see cref="Invoice"/> from the <see cref="RateQuote"/>
/// produced by <see cref="RatingStage"/>: a single freight line for a
/// <see cref="ServiceLevel.Standard"/> booking, or a freight line plus a
/// separate surcharge line for a <see cref="ServiceLevel.Express"/> one. The
/// invoice total always equals the quoted amount.
/// </summary>
public sealed class InvoiceStage : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var booking = context.Booking;
        var quote = context.Quote ?? throw new InvalidOperationException(
            "InvoiceStage requires a quote; run RatingStage first.");

        var builder = new InvoiceBuilder();
        if (booking.Service == ServiceLevel.Express)
        {
            var standardBase = new StandardRatingStrategy().Quote(booking).Amount;
            var surcharge = quote.Amount - standardBase;
            builder.AddLineItem("Freight", standardBase);
            builder.AddLineItem("Express service surcharge", surcharge);
        }
        else
        {
            builder.AddLineItem("Freight", quote.Amount);
        }

        return context with { Invoice = builder.Build(booking.ConsignmentId) };
    }
}
