namespace CarrierProvisioning;

/// <summary>
/// A second product of the Abstract Factory family: shapes a
/// carrier-agnostic <see cref="ShipmentRequest"/> into the
/// <see cref="CarrierRequest"/> payload a specific carrier's gateway expects.
/// </summary>
public interface IShipmentRequestBuilder
{
    CarrierRequest Build(ShipmentRequest request);
}
