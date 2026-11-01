namespace ConcurrentHandling;

/// <summary>
/// An in-memory stand-in for a durable handling ledger. Sequence assignment
/// uses <see cref="Interlocked.Increment(ref int)"/> so concurrent appends
/// never hand out the same sequence number twice; the backing list is guarded
/// by a plain <c>lock</c> because the append itself is a synchronous,
/// non-awaited mutation. <see cref="Entries"/> returns a defensive copy, so a
/// reader can never observe the list mid-mutation or mutate it back.
/// </summary>
public sealed class InMemoryHandlingLedger : IHandlingLedger
{
    private readonly List<HandlingEvent> _entries = [];
    private int _sequence;

    public async Task<int> AppendAsync(HandlingEvent e, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        var seq = Interlocked.Increment(ref _sequence);
        lock (_entries)
        {
            _entries.Add(e);
        }

        return seq;
    }

    public IReadOnlyList<HandlingEvent> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToArray();
            }
        }
    }
}
