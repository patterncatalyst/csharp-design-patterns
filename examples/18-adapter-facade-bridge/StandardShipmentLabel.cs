namespace CarrierIntegration;

/// <summary>
/// A refined abstraction: the outbound label for a forward shipment,
/// addressed to the destination.
/// </summary>
public sealed class StandardShipmentLabel(ILabelRenderer renderer, CarrierQuote quote, ShipmentRequest request)
    : ShipmentLabel(renderer)
{
    protected override LabelContent BuildContent() =>
        new("Standard Shipment", quote.CarrierCode, request.Destination, "Deliver during business hours.");
}
