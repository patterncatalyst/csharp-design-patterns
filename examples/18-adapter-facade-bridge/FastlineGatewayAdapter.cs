namespace CarrierIntegration;

/// <summary>
/// Adapts <see cref="FastlineSoapClient"/> to <see cref="ICarrierGateway"/>.
/// Fastline already prices in cents, so the amount passes straight through
/// <see cref="Money"/>'s constructor; only the weight unit (kilograms to
/// grams) and the request/response shapes need translating.
/// </summary>
public sealed class FastlineGatewayAdapter(FastlineSoapClient client) : ICarrierGateway
{
    public CarrierQuote GetQuote(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        int grams = (int)(request.Weight.Value * 1000m);
        FastlineQuoteResponse response = client.SubmitQuote(request.Origin, request.Destination, grams);

        return new CarrierQuote("FASTLINE", new Money(response.AmountCents), response.DaysInTransit);
    }
}
