namespace CarrierIntegration;

/// <summary>
/// The target interface every Adapter in this example implements. A caller
/// that holds an <see cref="ICarrierGateway"/> never needs to know whether
/// the carrier behind it speaks SOAP, REST, or a flat-file legacy protocol.
/// </summary>
public interface ICarrierGateway
{
    CarrierQuote GetQuote(ShipmentRequest request);
}
