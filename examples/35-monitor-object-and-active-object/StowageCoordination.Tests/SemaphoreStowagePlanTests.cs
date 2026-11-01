using System.Collections.Concurrent;
using Xunit;

namespace StowageCoordination.Tests;

/// <summary>
/// Proves the Stage 3 semaphore gate serializes reservations across the
/// asynchronous ledger write, and that it honours cancellation before ever
/// touching the servant.
/// </summary>
public class SemaphoreStowagePlanTests
{
    private const int Load = 500;

    private static List<SlotId> MakeSlots(int n) =>
        Enumerable.Range(1, n).Select(b => new SlotId(b, 1, 1)).ToList();

    private static SlotRequirements Dry => new(Reefer: false, Hazardous: false, WeightKg: 1000);

    [Fact]
    public async Task Semaphore_plan_serializes_reservations_across_the_async_ledger_write()
    {
        var servant = new StowagePlan(MakeSlots(Load * 2));
        using var semaphorePlan = new SemaphoreStowagePlan(servant, new InMemoryStowageLedger());
        var results = new ConcurrentBag<SlotAssignment>();

        await Task.WhenAll(Enumerable.Range(0, Load).Select(async i =>
        {
            var assignment = await semaphorePlan.ReserveAsync(
                new ConsignmentId($"C{i}"), Dry, TestContext.Current.CancellationToken);
            if (assignment is not null) results.Add(assignment);
        }));

        var slots = results.Select(r => r.Slot).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.Equal(servant.Count, results.Count);
    }

    [Fact]
    public async Task Semaphore_plan_honours_a_cancelled_reservation_token()
    {
        var servant = new StowagePlan(MakeSlots(10));
        using var semaphorePlan = new SemaphoreStowagePlan(servant, new InMemoryStowageLedger());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => semaphorePlan.ReserveAsync(new ConsignmentId("C0"), Dry, cts.Token));
    }
}
