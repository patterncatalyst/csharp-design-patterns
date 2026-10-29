namespace RateCalculation;

/// <summary>
/// Records a finalized quote for compliance. Kept separate from pricing and
/// tax so swapping the audit destination (a log, a queue, a database) never
/// touches either (SRP). Tests substitute this with NSubstitute rather than
/// writing to a real destination.
/// </summary>
public interface IAuditSink
{
    Task RecordQuoteAsync(Consignment consignment, Money finalQuote, CancellationToken cancellationToken = default);
}
