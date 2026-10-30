using System.Globalization;

namespace CarrierProvisioning;

/// <summary>
/// Fastline's <see cref="IShipmentRequestBuilder"/>. The resulting
/// <see cref="CarrierRequest.Carrier"/> is always <see cref="Fastline.Code"/>,
/// stamped from the reified carrier type rather than passed in by the
/// caller.
/// </summary>
public sealed class FastlineRequestBuilder : IShipmentRequestBuilder
{
    public CarrierRequest Build(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var weight = request.WeightKilograms.ToString(CultureInfo.InvariantCulture);
        var payload = $"{request.Origin}>{request.Destination}:{weight}";

        return new CarrierRequest(Fastline.Code, payload);
    }
}
