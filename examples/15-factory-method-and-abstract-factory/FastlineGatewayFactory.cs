namespace CarrierProvisioning;

/// <summary>
/// The Abstract Factory for Fastline's product family.
/// </summary>
public sealed class FastlineGatewayFactory : ICarrierGatewayFactory<Fastline>
{
    public ICarrierGateway CreateGateway() => new FastlineGateway();

    public IShipmentRequestBuilder CreateRequestBuilder() => new FastlineRequestBuilder();

    public IShipmentResponseParser CreateParser() => new FastlineResponseParser();
}
