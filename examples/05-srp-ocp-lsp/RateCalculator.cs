namespace RateCalculation;

/// <summary>
/// Orchestrates a full rate quote: price it, tax it, and (optionally) audit
/// it. Each step is an independently constructible collaborator; tax and
/// audit are optional, so pricing alone is always usable without them (SRP).
/// </summary>
public sealed class RateCalculator(IRateQuote pricing, ITaxPolicy? taxPolicy = null, IAuditSink? auditSink = null)
{
    public async Task<Money> QuoteAsync(Consignment consignment, CancellationToken cancellationToken = default)
    {
        var subtotal = pricing.Quote(consignment);
        var total = taxPolicy is null ? subtotal : taxPolicy.ApplyTax(subtotal);

        if (auditSink is not null)
        {
            await auditSink.RecordQuoteAsync(consignment, total, cancellationToken).ConfigureAwait(false);
        }

        return total;
    }
}
