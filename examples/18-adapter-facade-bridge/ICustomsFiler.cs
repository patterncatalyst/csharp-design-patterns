namespace CarrierIntegration;

/// <summary>
/// The third subsystem behind <see cref="ShipmentBookingFacade"/>: files the
/// customs declaration for a quoted shipment and returns a booking
/// reference.
/// </summary>
public interface ICustomsFiler
{
    string File(ShipmentRequest request, CarrierQuote quote);
}
