using System.Globalization;

namespace CarrierProvisioning;

/// <summary>
/// Airco's <see cref="IShipmentRequestBuilder"/>. The resulting
/// <see cref="CarrierRequest.Carrier"/> is always <see cref="Airco.Code"/>,
/// stamped from the reified carrier type rather than passed in by the
/// caller.
/// </summary>
public sealed class AircoRequestBuilder : IShipmentRequestBuilder
{
    public CarrierRequest Build(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var weight = request.WeightKilograms.ToString(CultureInfo.InvariantCulture);
        var payload = $"{request.Origin}>{request.Destination}:{weight}";

        return new CarrierRequest(Airco.Code, payload);
    }
}
