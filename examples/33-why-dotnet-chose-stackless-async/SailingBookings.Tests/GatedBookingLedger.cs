using System.Collections.Concurrent;

namespace SailingBookings.Tests;

/// <summary>
/// An <see cref="IBookingLedger"/> test double that suspends every
/// confirmation behind a single shared gate, so a test can push many
/// confirmations in flight at once, observe that they all arrived and
/// suspended, and then release them together. Every mechanism here is a
/// deterministic signal, never a wall-clock wait:
/// <list type="bullet">
/// <item>a <see cref="TaskCompletionSource"/> gate, released on demand by the
/// test, that every confirmation awaits;</item>
/// <item>an <see cref="Interlocked"/> arrival counter paired with a second
/// <see cref="TaskCompletionSource"/> that completes the instant the
/// configured target arrival count is reached;</item>
/// <item>a <see cref="ConcurrentDictionary{TKey,TValue}"/> recording the
/// managed thread id captured in the pre-await portion of
/// <see cref="ConfirmAsync"/>, before the gate is ever awaited.</item>
/// </list>
/// </summary>
public sealed class GatedBookingLedger(int target) : IBookingLedger
{
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _allArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ConcurrentDictionary<int, byte> _preAwaitThreadIds = new();

    private int _arrived;
    private int _sequence;

    /// <summary>Completes once <paramref name="target"/> confirmations have arrived.</summary>
    public Task AllArrived => _allArrived.Task;

    /// <summary>The number of confirmations that have reached the gate.</summary>
    public int ArrivedCount => Volatile.Read(ref _arrived);

    /// <summary>
    /// The count of distinct managed thread ids seen in the pre-await portion
    /// of <see cref="ConfirmAsync"/>, across every confirmation observed so far.
    /// </summary>
    public int DistinctPreAwaitThreadCount => _preAwaitThreadIds.Count;

    /// <summary>Releases every confirmation currently suspended at the gate.</summary>
    public void Release() => _release.TrySetResult();

    public async Task<int> ConfirmAsync(string consignmentId, CancellationToken ct)
    {
        // Captured synchronously, before the first await: this is the thread
        // that enumerated the caller's Select(...).ToList(), never a pool
        // thread the state machine happened to resume on.
        _preAwaitThreadIds.TryAdd(Environment.CurrentManagedThreadId, 0);

        if (Interlocked.Increment(ref _arrived) == target)
        {
            _allArrived.TrySetResult();
        }

        await _release.Task.WaitAsync(ct);
        return Interlocked.Increment(ref _sequence);
    }
}
