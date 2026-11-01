namespace StowageCoordination;

/// <summary>
/// A ledger that stands in for durable I/O with an in-memory list. The
/// <see cref="Task.Yield"/> forces a real asynchronous continuation without
/// any wall-clock wait, so callers still observe the append as a point where
/// control returns to the scheduler before the slot entry lands.
/// </summary>
public sealed class InMemoryStowageLedger : IStowageLedger
{
    private readonly List<SlotAssignment> _entries = [];

    public IReadOnlyList<SlotAssignment> Entries => _entries;

    public async Task AppendAsync(SlotAssignment assignment, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        lock (_entries)
        {
            _entries.Add(assignment);
        }
    }
}
