using Xunit;

namespace SailingBookings.Tests;

public sealed class BookingDeskTests
{
    [Fact]
    public async Task Accepting_a_booking_returns_a_receipt_from_the_ledger()
    {
        var desk = new SailingBookingDesk(new InMemoryBookingLedger(), sailingCapacity: 10);
        var request = new BookingRequest("CONS-0001");

        var receipt = await desk.AcceptAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(request.ConsignmentId, receipt.ConsignmentId);
        Assert.True(receipt.LedgerSequence >= 1);
    }

    [Fact]
    public async Task Every_booking_receives_a_distinct_ledger_sequence()
    {
        const int count = 1000;
        var desk = new SailingBookingDesk(new InMemoryBookingLedger(), sailingCapacity: count);
        var requests = Enumerable.Range(1, count)
            .Select(i => new BookingRequest($"CONS-{i:D4}"))
            .ToList();

        var receipts = await desk.AcceptAllAsync(requests, TestContext.Current.CancellationToken);

        var sequences = receipts.Select(r => r.LedgerSequence).ToHashSet();
        Assert.Equal(count, sequences.Count);
        Assert.Equal(Enumerable.Range(1, count).ToHashSet(), sequences);
    }

    [Fact]
    public async Task A_capacity_check_completes_synchronously_without_allocating_a_task()
    {
        var desk = new SailingBookingDesk(new InMemoryBookingLedger(), sailingCapacity: 10);

        var vt = desk.HasCapacityAsync(TestContext.Current.CancellationToken);

        Assert.True(vt.IsCompletedSuccessfully);
        Assert.True(await vt);
    }

    [Fact]
    public async Task A_cancelled_booking_token_is_honoured()
    {
        var desk = new SailingBookingDesk(new InMemoryBookingLedger(), sailingCapacity: 10);
        var request = new BookingRequest("CONS-0001");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await desk.AcceptAsync(request, cts.Token));
    }
}
