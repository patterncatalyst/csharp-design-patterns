namespace StowageCoordination.Tests;

/// <summary>
/// A ledger that signals <see cref="Entered"/> the instant a write starts and
/// then blocks until the test calls <see cref="Release"/>. Lets a test pin
/// the Active Object's scheduler mid-write so it can assert on a known,
/// deterministic state (a full channel, a cancelled queued request) without
/// any wall-clock wait.
/// </summary>
public sealed class GatedStowageLedger : IStowageLedger
{
    private readonly TaskCompletionSource _entered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Release() => _release.TrySetResult();

    public async Task AppendAsync(SlotAssignment assignment, CancellationToken ct)
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(ct);
    }
}
