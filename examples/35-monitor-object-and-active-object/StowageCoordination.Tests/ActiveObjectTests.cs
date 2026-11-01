using System.Collections.Concurrent;
using Xunit;

namespace StowageCoordination.Tests;

/// <summary>
/// Proves the Stage 4 Active Object serializes high-volume concurrent load,
/// applies real backpressure once its bounded channel fills, honours
/// cancellation on a request still waiting in the queue, and keeps its
/// scheduler loop alive after a ledger failure.
/// </summary>
public class ActiveObjectTests
{
    private const int Load = 1_000;

    private static List<SlotId> MakeSlots(int n) =>
        Enumerable.Range(1, n).Select(b => new SlotId(b, 1, 1)).ToList();

    private static SlotRequirements Dry => new(Reefer: false, Hazardous: false, WeightKg: 1000);

    [Fact]
    public async Task Active_object_serializes_a_thousand_reservations_without_a_slot_twice()
    {
        var servant = new StowagePlan(MakeSlots(Load * 2));
        await using var active = new StowageActiveObject(servant, new InMemoryStowageLedger(), capacity: 64);
        var results = new ConcurrentBag<SlotAssignment>();

        await Task.WhenAll(Enumerable.Range(0, Load).Select(async i =>
        {
            var assignment = await active.ReserveAsync(
                new ConsignmentId($"C{i}"), Dry, TestContext.Current.CancellationToken);
            if (assignment is not null) results.Add(assignment);
        }));

        Assert.Equal(Load, results.Count);
        var slots = results.Select(r => r.Slot).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
    }

    [Fact]
    public async Task A_full_bounded_channel_applies_backpressure_until_the_scheduler_drains()
    {
        var gate = new GatedStowageLedger();
        var servant = new StowagePlan(MakeSlots(100));
        await using var active = new StowageActiveObject(servant, gate, capacity: 1);

        var a = await active.EnqueueAsync(
            new ConsignmentId("A"), Dry, TestContext.Current.CancellationToken);
        await gate.Entered.WaitAsync(TestContext.Current.CancellationToken); // A is now in-flight inside the scheduler

        var b = await active.EnqueueAsync(
            new ConsignmentId("B"), Dry, TestContext.Current.CancellationToken); // fills the capacity-1 channel

        var pendingC = active.EnqueueAsync(
            new ConsignmentId("C"), Dry, TestContext.Current.CancellationToken); // not awaited: channel is full
        Assert.False(pendingC.IsCompleted);

        gate.Release();

        var c = await pendingC;
        Assert.NotNull(await a);
        Assert.NotNull(await b);
        Assert.NotNull(await c);
    }

    [Fact]
    public async Task A_queued_reservation_honours_a_cancelled_token()
    {
        var gate = new GatedStowageLedger();
        var servant = new StowagePlan(MakeSlots(100));
        await using var active = new StowageActiveObject(servant, gate, capacity: 8);

        _ = active.ReserveAsync(
            new ConsignmentId("A"), Dry, TestContext.Current.CancellationToken); // holds the scheduler on the gate
        await gate.Entered.WaitAsync(TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        var cancelled = active.ReserveAsync(new ConsignmentId("B"), Dry, cts.Token); // still queued
        await cts.CancelAsync();
        gate.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    }

    [Fact]
    public async Task A_ledger_failure_surfaces_to_the_caller_and_the_scheduler_survives()
    {
        var ledger = new ThrowOnceStowageLedger();
        var servant = new StowagePlan(MakeSlots(100));
        await using var active = new StowageActiveObject(servant, ledger, capacity: 8);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => active.ReserveAsync(
                new ConsignmentId("boom"), Dry, TestContext.Current.CancellationToken));

        var after = await active.ReserveAsync(
            new ConsignmentId("ok"), Dry, TestContext.Current.CancellationToken);
        Assert.NotNull(after);
    }
}
