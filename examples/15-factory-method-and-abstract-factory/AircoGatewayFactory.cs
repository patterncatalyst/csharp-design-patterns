namespace CarrierProvisioning;

/// <summary>
/// The Abstract Factory for Airco's product family. Airco is added after
/// Fastline and Seabright are already registered; this factory, plus one
/// <c>Register&lt;Airco&gt;</c> call, is the entire cost of adding it.
/// </summary>
public sealed class AircoGatewayFactory : ICarrierGatewayFactory<Airco>
{
    public ICarrierGateway CreateGateway() => new AircoGateway();

    public IShipmentRequestBuilder CreateRequestBuilder() => new AircoRequestBuilder();

    public IShipmentResponseParser CreateParser() => new AircoResponseParser();
}
