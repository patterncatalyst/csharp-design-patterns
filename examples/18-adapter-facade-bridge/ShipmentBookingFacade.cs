namespace CarrierIntegration;

/// <summary>
/// A Facade over three subsystems a caller would otherwise have to
/// orchestrate itself: address validation, carrier quoting, and customs
/// filing. <see cref="Book"/> is the one call a booking screen needs,
/// regardless of which carrier, validator, or filer sit behind it.
/// </summary>
public sealed class ShipmentBookingFacade(IAddressValidator validator, ICarrierGateway gateway, ICustomsFiler filer)
{
    public ShipmentConfirmation Book(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        validator.Validate(request);
        CarrierQuote quote = gateway.GetQuote(request);
        string bookingReference = filer.File(request, quote);

        return new ShipmentConfirmation(quote.CarrierCode, bookingReference, quote.Amount);
    }
}
