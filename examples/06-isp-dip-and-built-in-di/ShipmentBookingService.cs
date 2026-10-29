namespace CarrierGateways;

/// <summary>
/// Books a shipment and reports back its tracking status. Depends on the
/// full <see cref="ICarrierGateway"/> abstraction, never a concrete gateway:
/// swapping <see cref="RestCarrierGateway"/> for <see cref="FakeCarrierGateway"/>
/// (or any future implementation) changes only the composition root's
/// registration, never this class (Dependency Inversion Principle).
/// </summary>
public sealed class ShipmentBookingService(ICarrierGateway gateway)
{
    public Task<BookingReceipt> BookShipmentAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken) =>
        gateway.BookAsync(destination, weightKilograms, cancellationToken);

    public Task<string> CheckStatusAsync(string trackingNumber, CancellationToken cancellationToken) =>
        gateway.GetTrackingStatusAsync(trackingNumber, cancellationToken);
}
