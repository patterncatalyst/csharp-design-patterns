namespace CarrierProvisioning;

/// <summary>
/// The third product of the Abstract Factory family: turns a carrier's raw
/// response string back into a carrier-agnostic
/// <see cref="ShipmentConfirmation"/>.
/// </summary>
public interface IShipmentResponseParser
{
    ShipmentConfirmation Parse(string rawResponse);
}
