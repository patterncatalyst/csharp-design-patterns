namespace CarrierIntegration;

/// <summary>
/// A minimal customs filer: derives a deterministic booking reference from
/// the shipment and its quote. A real filer would submit a declaration to a
/// customs authority and hand back whatever reference it assigns; this
/// example keeps the behavior synchronous and predictable.
/// </summary>
public sealed class CustomsFiler : ICustomsFiler
{
    public string File(ShipmentRequest request, CarrierQuote quote)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(quote);

        return $"{quote.CarrierCode}-{request.Origin}-{request.Destination}-{quote.TransitDays:D2}"
            .ToUpperInvariant();
    }
}
