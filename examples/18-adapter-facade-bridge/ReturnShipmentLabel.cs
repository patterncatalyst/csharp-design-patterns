namespace CarrierIntegration;

/// <summary>
/// A refined abstraction: the label for a shipment returning to its
/// original sender, addressed back to the origin.
/// </summary>
public sealed class ReturnShipmentLabel(ILabelRenderer renderer, CarrierQuote quote, ShipmentRequest request)
    : ShipmentLabel(renderer)
{
    protected override LabelContent BuildContent() =>
        new("Return Shipment", quote.CarrierCode, request.Origin, "Return to sender on refusal.");
}
