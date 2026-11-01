using System.Collections.Concurrent;
using Xunit;

namespace ConcurrentHandling.Tests;

/// <summary>
/// Proves immutability is the strategy that makes every other pattern in this
/// chapter safe to share across threads: many readers observe the same
/// <see cref="HandlingEvent"/> concurrently without a lock, each produces its
/// own independent <c>with</c>-copy, and the original is untouched by any of
/// them.
/// </summary>
public class ImmutabilityTests
{
    private const int ReaderCount = 16;

    [Fact]
    public async Task An_immutable_handling_event_is_safe_to_share_across_threads()
    {
        var tracking = new TrackingId("C-IMM-1");
        var original = new HandlingEvent(tracking, HandlingKind.Claim, 0);
        using var barrier = new Barrier(ReaderCount);
        var copies = new ConcurrentBag<HandlingEvent>();

        var readers = Enumerable.Range(0, ReaderCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();

            Assert.Equal(tracking, original.Tracking);
            Assert.Equal(HandlingKind.Claim, original.Kind);
            Assert.Equal(0, original.Sequence);

            var copy = original with { Sequence = i + 1 };
            copies.Add(copy);
        }));

        await Task.WhenAll(readers);

        Assert.Equal(tracking, original.Tracking);
        Assert.Equal(HandlingKind.Claim, original.Kind);
        Assert.Equal(0, original.Sequence);

        Assert.Equal(ReaderCount, copies.Count);
        Assert.Equal(
            Enumerable.Range(1, ReaderCount).ToList(),
            copies.Select(c => c.Sequence).OrderBy(s => s).ToList());
        Assert.All(copies, copy => Assert.Equal(original with { Sequence = copy.Sequence }, copy));
    }
}
