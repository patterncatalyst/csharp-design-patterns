using System.Collections.Concurrent;
using Xunit;

namespace StowageCoordination.Tests;

/// <summary>
/// Proves the Stage 1 Monitor Object serializes concurrent reservations, and
/// contrasts it with the bare servant it wraps under the same load pattern
/// run sequentially.
/// </summary>
public class MonitorObjectTests
{
    private const int ReaderCount = 16;

    private static List<SlotId> MakeSlots(int n) =>
        Enumerable.Range(1, n).Select(b => new SlotId(b, 1, 1)).ToList();

    private static SlotRequirements Dry => new(Reefer: false, Hazardous: false, WeightKg: 1000);

    [Fact]
    public async Task Synchronized_plan_hands_out_every_slot_at_most_once_under_concurrent_load()
    {
        var plan = new StowagePlan(MakeSlots(ReaderCount * 2));
        var synchronized = new SynchronizedStowagePlan(plan);
        using var barrier = new Barrier(ReaderCount);
        var results = new ConcurrentBag<SlotAssignment>();

        var readers = Enumerable.Range(0, ReaderCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var assignment = synchronized.Reserve(new ConsignmentId($"C{i}"), Dry);
            if (assignment is not null) results.Add(assignment);
        }));

        await Task.WhenAll(readers);

        var slots = results.Select(r => r.Slot).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.Equal(plan.Count, results.Count);
    }

    [Fact]
    public void Unsynchronized_servant_reserves_distinct_slots_in_sequence()
    {
        var plan = new StowagePlan(MakeSlots(ReaderCount * 2));
        var results = new List<SlotAssignment>();

        for (var i = 0; i < ReaderCount; i++)
        {
            var assignment = plan.TryReserve(new ConsignmentId($"C{i}"), Dry);
            if (assignment is not null) results.Add(assignment);
        }

        var slots = results.Select(r => r.Slot).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.Equal(plan.Count, results.Count);
    }
}
