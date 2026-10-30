namespace CarrierIntegration;

/// <summary>
/// The one request shape every carrier gateway in this example accepts,
/// regardless of what the carrier it talks to actually expects on the wire.
/// </summary>
public sealed record ShipmentRequest(string Origin, string Destination, Kilograms Weight);
