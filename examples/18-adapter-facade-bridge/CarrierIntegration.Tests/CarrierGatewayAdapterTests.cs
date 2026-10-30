using System.Globalization;
using Xunit;

namespace CarrierIntegration.Tests;

/// <summary>
/// Proves each Adapter maps its carrier's native call and response shape
/// onto <see cref="ICarrierGateway"/> correctly. Each test calls the fake
/// native client directly with the same inputs the adapter would use, so
/// the expected <see cref="CarrierQuote"/> is derived from the carrier's own
/// deterministic answer rather than a value hardcoded against the adapter's
/// internal arithmetic.
/// </summary>
public class CarrierGatewayAdapterTests
{
    [Fact]
    public void FastlineGatewayAdapter_maps_the_SOAP_response_to_a_carrier_quote()
    {
        var client = new FastlineSoapClient();
        var request = new ShipmentRequest("ORD", "DXB", new Kilograms(5m));
        var adapter = new FastlineGatewayAdapter(client);

        CarrierQuote quote = adapter.GetQuote(request);

        int expectedGrams = (int)(request.Weight.Value * 1000m);
        FastlineQuoteResponse native = client.SubmitQuote(request.Origin, request.Destination, expectedGrams);

        Assert.Equal("FASTLINE", quote.CarrierCode);
        Assert.Equal(native.DaysInTransit, quote.TransitDays);
        Assert.Equal(new Money(native.AmountCents).ToDecimal(), quote.Amount.ToDecimal());
    }

    [Fact]
    public void SeabrightGatewayAdapter_maps_the_REST_response_to_a_carrier_quote()
    {
        var client = new SeabrightRestClient();
        var request = new ShipmentRequest("HKG", "RTM", new Kilograms(10m));
        var adapter = new SeabrightGatewayAdapter(client);

        CarrierQuote quote = adapter.GetQuote(request);

        var nativeRequest = new SeabrightRateRequest(request.Origin, request.Destination, request.Weight.Value);
        SeabrightQuoteDto native = client.RequestRate(nativeRequest);

        Assert.Equal("SEABRIGHT", quote.CarrierCode);
        Assert.Equal(native.EstimatedDays, quote.TransitDays);
        Assert.Equal(Money.FromDecimal(native.RateDollars).ToDecimal(), quote.Amount.ToDecimal());
    }

    [Fact]
    public void AircoGatewayAdapter_maps_the_flat_file_response_to_a_carrier_quote()
    {
        var client = new AircoLegacyClient();
        var request = new ShipmentRequest("LAX", "JFK", new Kilograms(4m));
        var adapter = new AircoGatewayAdapter(client);

        CarrierQuote quote = adapter.GetQuote(request);

        decimal expectedPounds = ((Pounds)request.Weight).Value;
        string[] native = client.Quote(
        [
            request.Origin,
            request.Destination,
            expectedPounds.ToString(CultureInfo.InvariantCulture),
        ]);

        Assert.Equal("AIRCO", quote.CarrierCode);
        Assert.Equal(int.Parse(native[1], CultureInfo.InvariantCulture), quote.TransitDays);
        Assert.Equal(
            Money.FromDecimal(decimal.Parse(native[0], CultureInfo.InvariantCulture)).ToDecimal(),
            quote.Amount.ToDecimal());
    }
}
