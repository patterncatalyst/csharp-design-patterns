namespace RatingEngine;

/// <summary>
/// Quotes a shipment by combining a carrier's base rate, a tax policy, and an
/// audit sink. The three collaborators arrive through the constructor
/// (constructor injection), so a test substitutes exactly the ones it cares
/// about and leaves the rest alone.
/// </summary>
public sealed class ShipmentRatingEngine(ICarrierRates carrierRates, ITaxPolicy taxPolicy, IAuditSink auditSink)
{
    public async Task<Money> QuoteAsync(Consignment consignment, CancellationToken cancellationToken)
    {
        var baseRate = await carrierRates
            .GetBaseRateAsync(consignment.Destination, consignment.Kilograms, cancellationToken)
            .ConfigureAwait(false);

        var quoted = taxPolicy.ApplyTax(baseRate);

        await auditSink
            .RecordAsync(consignment.ConsignmentId, quoted, cancellationToken)
            .ConfigureAwait(false);

        return quoted;
    }
}
