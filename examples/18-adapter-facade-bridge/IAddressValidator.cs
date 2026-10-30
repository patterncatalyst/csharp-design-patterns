namespace CarrierIntegration;

/// <summary>
/// The first subsystem behind <see cref="ShipmentBookingFacade"/>: confirms
/// a shipment's origin and destination are recognized addresses before any
/// carrier is asked for a quote.
/// </summary>
public interface IAddressValidator
{
    void Validate(ShipmentRequest request);
}
