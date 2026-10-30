namespace CarrierIntegration;

/// <summary>
/// Adapts <see cref="SeabrightRestClient"/> to <see cref="ICarrierGateway"/>.
/// Seabright already works in kilograms, so the weight passes through
/// unchanged; the dollar rate it returns is narrowed to <see cref="Money"/>
/// with the named <see cref="Money.FromDecimal"/> conversion.
/// </summary>
public sealed class SeabrightGatewayAdapter(SeabrightRestClient client) : ICarrierGateway
{
    public CarrierQuote GetQuote(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        SeabrightRateRequest nativeRequest = new(request.Origin, request.Destination, request.Weight.Value);
        SeabrightQuoteDto response = client.RequestRate(nativeRequest);

        return new CarrierQuote("SEABRIGHT", Money.FromDecimal(response.RateDollars), response.EstimatedDays);
    }
}
