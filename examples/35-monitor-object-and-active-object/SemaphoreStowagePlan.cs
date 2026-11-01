namespace StowageCoordination;

/// <summary>
/// STAGE 3 - an async-aware gate. A reservation is not confirmed until its
/// <see cref="IStowageLedger.AppendAsync"/> write lands, and that write is a
/// genuine <c>await</c>. A plain <c>lock</c> statement cannot wrap an
/// <c>await</c> (it is a compile error), so this stage swaps the monitor for
/// a <see cref="SemaphoreSlim"/> sized to one, which can be awaited.
/// </summary>
public sealed class SemaphoreStowagePlan : IDisposable
{
    private readonly StowagePlan _servant;
    private readonly IStowageLedger _ledger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SemaphoreStowagePlan(StowagePlan servant, IStowageLedger ledger)
    {
        _servant = servant;
        _ledger = ledger;
    }

    public async Task<SlotAssignment?> ReserveAsync(
        ConsignmentId id, SlotRequirements req, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var assignment = _servant.TryReserve(id, req);
            if (assignment is not null)
                await _ledger.AppendAsync(assignment, ct);
            return assignment;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
