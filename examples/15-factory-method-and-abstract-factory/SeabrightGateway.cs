using System.Globalization;

namespace CarrierProvisioning;

/// <summary>
/// Seabright's <see cref="ICarrierGateway"/>. A real implementation calls
/// out over HTTP; this one derives a deterministic raw response from the
/// payload's length so the test suite needs no network.
/// </summary>
public sealed class SeabrightGateway : ICarrierGateway
{
    public string Send(CarrierRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var length = request.Payload.Length.ToString("D4", CultureInfo.InvariantCulture);
        return $"{Seabright.Code}|{length}";
    }
}
