namespace CarrierIntegration;

/// <summary>
/// The single result <see cref="ShipmentBookingFacade.Book"/> hands back,
/// after validation, quoting, and customs filing have all run.
/// </summary>
public sealed record ShipmentConfirmation(string CarrierCode, string BookingReference, Money Charged);
