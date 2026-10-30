namespace CarrierProvisioning;

/// <summary>
/// The result of dispatching a <see cref="ShipmentRequest"/> through a
/// carrier gateway: which carrier accepted it, and the tracking number it
/// assigned.
/// </summary>
public sealed record ShipmentConfirmation(string Carrier, string TrackingNumber);
