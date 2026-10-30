namespace CarrierProvisioning;

/// <summary>
/// The Abstract Factory role: produces a matched family of products for one
/// carrier, a <see cref="ICarrierGateway"/>, a <see cref="IShipmentRequestBuilder"/>,
/// and a <see cref="IShipmentResponseParser"/>, so a caller never has to
/// build one piece of the family against a different carrier's other pieces.
/// Generic over <typeparamref name="TCarrier"/> so
/// <see cref="CarrierRegistry"/> can key a factory by the carrier's own
/// type rather than a string.
/// </summary>
public interface ICarrierGatewayFactory<TCarrier>
    where TCarrier : ICarrier
{
    ICarrierGateway CreateGateway();

    IShipmentRequestBuilder CreateRequestBuilder();

    IShipmentResponseParser CreateParser();
}
