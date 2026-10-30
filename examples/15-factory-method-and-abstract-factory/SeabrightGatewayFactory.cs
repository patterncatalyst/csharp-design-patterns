namespace CarrierProvisioning;

/// <summary>
/// The Abstract Factory for Seabright's product family.
/// </summary>
public sealed class SeabrightGatewayFactory : ICarrierGatewayFactory<Seabright>
{
    public ICarrierGateway CreateGateway() => new SeabrightGateway();

    public IShipmentRequestBuilder CreateRequestBuilder() => new SeabrightRequestBuilder();

    public IShipmentResponseParser CreateParser() => new SeabrightResponseParser();
}
