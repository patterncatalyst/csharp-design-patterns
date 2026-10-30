namespace CarrierProvisioning;

/// <summary>
/// A shipment to hand off to a carrier: where it originates, where it is
/// going, and how much it weighs. Carrier-agnostic; a
/// <see cref="IShipmentRequestBuilder"/> turns it into the wire shape a
/// specific carrier expects.
/// </summary>
public sealed record ShipmentRequest(string Origin, string Destination, decimal WeightKilograms);
