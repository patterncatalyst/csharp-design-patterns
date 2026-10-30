namespace CarrierProvisioning;

/// <summary>
/// Seabright's <see cref="IShipmentResponseParser"/>. Splits the gateway's
/// <c>"{code}|{checksum}"</c> raw response back into a
/// <see cref="ShipmentConfirmation"/>.
/// </summary>
public sealed class SeabrightResponseParser : IShipmentResponseParser
{
    public ShipmentConfirmation Parse(string rawResponse)
    {
        ArgumentNullException.ThrowIfNull(rawResponse);

        var parts = rawResponse.Split('|');
        return new ShipmentConfirmation(parts[0], $"{parts[0]}-{parts[1]}");
    }
}
