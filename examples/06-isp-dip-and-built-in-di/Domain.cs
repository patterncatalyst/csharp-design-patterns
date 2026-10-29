namespace CarrierGateways;

/// <summary>
/// A monetary amount quoted or charged by a carrier. Defaults to US dollars
/// since every carrier in this example prices in the same currency; a
/// multi-currency carrier would widen this type, not change its callers.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency = "USD")
{
    public override string ToString() => $"{Amount:0.00} {Currency}";
}

/// <summary>
/// A price and transit-time estimate for carrying a shipment to a
/// destination. Returned by <see cref="ICarrierRates"/>.
/// </summary>
public sealed record RateQuote(Money Price, TimeSpan TransitTime);

/// <summary>
/// Proof that a carrier accepted a shipment: the tracking number it assigned
/// and when the booking was recorded. Returned by <see cref="ICarrierTracking"/>.
/// </summary>
public sealed record BookingReceipt(string TrackingNumber, DateTimeOffset BookedAtUtc);
