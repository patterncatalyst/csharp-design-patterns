namespace ConcurrentHandling;

/// <summary>
/// A concrete command that records one <see cref="HandlingEvent"/> against a
/// ledger (the Command pattern's receiver) and returns the resulting
/// <see cref="HandlingReceipt"/> as its result.
/// </summary>
public sealed class RecordHandlingCommand(HandlingEvent evt, IHandlingLedger ledger) : IHandlingCommand
{
    public async Task<HandlingReceipt> ExecuteAsync(CancellationToken ct)
    {
        var seq = await ledger.AppendAsync(evt, ct);
        return new HandlingReceipt(evt.Tracking, seq);
    }
}
