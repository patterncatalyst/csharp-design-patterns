using Xunit;

namespace SailingBookings.Tests;

/// <summary>
/// Proves the chapter's central claim directly: a suspended async method
/// costs a small heap-allocated state machine, not a parked thread. A
/// thousand bookings can all suspend at once, on the single thread that
/// started them, without a thread each.
/// </summary>
public sealed class StacklessModelTests
{
    [Fact]
    public async Task A_thousand_bookings_suspend_concurrently_without_a_thread_each()
    {
        const int count = 1000;
        var gated = new GatedBookingLedger(target: count);
        var desk = new SailingBookingDesk(gated, sailingCapacity: count);
        var requests = Enumerable.Range(1, count)
            .Select(i => new BookingRequest($"CONS-{i:D4}"))
            .ToList();
        var ct = TestContext.Current.CancellationToken;

        // Select(...).ToList() enumerates synchronously on this test thread,
        // calling AcceptAsync (and, through it, GatedBookingLedger.ConfirmAsync)
        // once per request before any of them has a chance to resume.
        var tasks = requests.Select(r => desk.AcceptAsync(r, ct)).ToList();

        await gated.AllArrived;

        Assert.Equal(count, gated.ArrivedCount);
        Assert.Equal(1, gated.DistinctPreAwaitThreadCount);
        Assert.All(tasks, t => Assert.False(t.IsCompleted));

        gated.Release();
        var receipts = await Task.WhenAll(tasks);

        Assert.Equal(count, receipts.Length);
    }

    [Fact]
    public async Task A_booking_resumes_and_completes_after_the_awaited_confirmation()
    {
        var gated = new GatedBookingLedger(target: 1);
        var desk = new SailingBookingDesk(gated, sailingCapacity: 1);
        var request = new BookingRequest("CONS-0001");
        var ct = TestContext.Current.CancellationToken;

        var task = desk.AcceptAsync(request, ct);

        await gated.AllArrived;
        Assert.False(task.IsCompleted);

        gated.Release();
        var receipt = await task;

        Assert.Equal(request.ConsignmentId, receipt.ConsignmentId);
        Assert.Equal(1, receipt.LedgerSequence);
    }
}
