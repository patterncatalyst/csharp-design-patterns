using System.Globalization;

namespace CarrierIntegration;

/// <summary>
/// Adapts <see cref="AircoLegacyClient"/> to <see cref="ICarrierGateway"/>.
/// Airco expects pounds rather than kilograms, so this adapter is the one
/// place that calls the explicit <c>(Pounds)</c> conversion on the request's
/// weight, and the one place that parses Airco's bare string-array reply
/// back into <see cref="Money"/> and an integer day count.
/// </summary>
public sealed class AircoGatewayAdapter(AircoLegacyClient client) : ICarrierGateway
{
    public CarrierQuote GetQuote(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Pounds weight = (Pounds)request.Weight;
        string[] record =
        [
            request.Origin,
            request.Destination,
            weight.Value.ToString(CultureInfo.InvariantCulture),
        ];

        string[] response = client.Quote(record);
        decimal amount = decimal.Parse(response[0], CultureInfo.InvariantCulture);
        int transitDays = int.Parse(response[1], CultureInfo.InvariantCulture);

        return new CarrierQuote("AIRCO", Money.FromDecimal(amount), transitDays);
    }
}
