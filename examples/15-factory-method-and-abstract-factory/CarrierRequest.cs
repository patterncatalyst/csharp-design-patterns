namespace CarrierProvisioning;

/// <summary>
/// The carrier-specific payload a <see cref="IShipmentRequestBuilder"/>
/// produces from a <see cref="ShipmentRequest"/>. <see cref="Carrier"/> is
/// the carrier's own code (<see cref="ICarrier.Code"/>), stamped by the
/// builder, not chosen by the caller.
/// </summary>
public sealed record CarrierRequest(string Carrier, string Payload);
