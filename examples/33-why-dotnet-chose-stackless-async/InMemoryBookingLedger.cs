namespace SailingBookings;

/// <summary>
/// An in-memory <see cref="IBookingLedger"/> that suspends with
/// <see cref="Task.Yield"/> rather than a wall-clock delay, so a caller sees
/// a real asynchronous suspend-and-resume without a test ever waiting on the
/// clock. The sequence counter is updated with <see cref="Interlocked"/> so
/// concurrent confirmations never race.
/// </summary>
public sealed class InMemoryBookingLedger : IBookingLedger
{
    private int _sequence;

    public async Task<int> ConfirmAsync(string consignmentId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        return Interlocked.Increment(ref _sequence);
    }
}
