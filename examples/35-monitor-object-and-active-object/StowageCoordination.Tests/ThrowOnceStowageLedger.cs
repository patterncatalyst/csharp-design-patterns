namespace StowageCoordination.Tests;

/// <summary>
/// A ledger that fails its first write and succeeds on every write after
/// that, so a test can prove a servant or ledger exception surfaces to the
/// caller that triggered it without taking down the Active Object's
/// scheduler loop for later callers.
/// </summary>
public sealed class ThrowOnceStowageLedger : IStowageLedger
{
    private int _calls;

    public Task AppendAsync(SlotAssignment assignment, CancellationToken ct)
    {
        if (Interlocked.Increment(ref _calls) == 1)
            throw new InvalidOperationException("ledger write failed");

        return Task.CompletedTask;
    }
}
