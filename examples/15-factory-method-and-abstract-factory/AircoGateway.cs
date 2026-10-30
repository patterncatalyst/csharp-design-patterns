using System.Globalization;

namespace CarrierProvisioning;

/// <summary>
/// Airco's <see cref="ICarrierGateway"/>. A real implementation calls out
/// over HTTP; this one derives a deterministic raw response from the
/// payload's length so the test suite needs no network.
/// </summary>
public sealed class AircoGateway : ICarrierGateway
{
    public string Send(CarrierRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var length = request.Payload.Length.ToString("D4", CultureInfo.InvariantCulture);
        return $"{Airco.Code}|{length}";
    }
}
