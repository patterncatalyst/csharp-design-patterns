namespace SailingBookings;

/// <summary>
/// Accepts bookings for a single sailing against an <see cref="IBookingLedger"/>.
/// Each accepted booking suspends the calling async state machine at the
/// ledger's await point and resumes it on completion; no desk method parks a
/// dedicated thread while a confirmation is in flight, which is the point of
/// a stackless async implementation: a thousand in-flight bookings cost a
/// thousand small state machines, not a thousand threads.
/// </summary>
public sealed class SailingBookingDesk(IBookingLedger ledger, int sailingCapacity)
{
    private int _accepted;

    public async Task<BookingReceipt> AcceptAsync(BookingRequest request, CancellationToken ct = default)
    {
        var seq = await ledger.ConfirmAsync(request.ConsignmentId, ct);
        Interlocked.Increment(ref _accepted);
        return new BookingReceipt(request.ConsignmentId, seq);
    }

    public async Task<IReadOnlyList<BookingReceipt>> AcceptAllAsync(
        IReadOnlyCollection<BookingRequest> requests, CancellationToken ct = default)
    {
        var tasks = requests.Select(r => AcceptAsync(r, ct)).ToList();
        return await Task.WhenAll(tasks);
    }

    public ValueTask<bool> HasCapacityAsync(CancellationToken ct = default) =>
        new(Volatile.Read(ref _accepted) < sailingCapacity);
}
