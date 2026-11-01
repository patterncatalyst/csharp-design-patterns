using Xunit;

namespace HandlingEventIngest.Tests;

/// <summary>
/// Proves a full bounded channel applies real backpressure to a producer:
/// once the channel is at capacity, <see cref="HandlingEventPipeline.SubmitAsync"/>
/// does not complete until the consumer makes room. The consumer here is
/// gated by a pair of <see cref="TaskCompletionSource{TResult}"/> instances
/// rather than any wall-clock wait, so the test deterministically drives the
/// pipeline into a known-full state before asserting a pending submission
/// has not completed.
/// </summary>
public class BackpressureTests
{
    [Fact]
    public async Task A_full_bounded_channel_applies_backpressure_to_the_producer()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask Gate(HandlingEvent e, CancellationToken ct)
        {
            entered.TrySetResult(true);
            await release.Task;
        }

        var pipeline = new HandlingEventPipeline(capacity: 1, consumerCount: 1, onEvent: Gate);
        var ct = TestContext.Current.CancellationToken;

        var a = new HandlingEvent(new TrackingId("CN-A"), HandlingKind.Receive, 0);
        var b = new HandlingEvent(new TrackingId("CN-B"), HandlingKind.Load, 1);
        var c = new HandlingEvent(new TrackingId("CN-C"), HandlingKind.Unload, 2);

        // A is dequeued by the single consumer and parked inside the gate,
        // leaving the capacity-1 channel empty.
        await pipeline.SubmitAsync(a, ct);
        await entered.Task;

        // B now lands in the otherwise-empty channel, filling it to capacity.
        await pipeline.SubmitAsync(b, ct);

        // C cannot be written until the consumer drains B, so this submission
        // must still be pending.
        var pendingC = pipeline.SubmitAsync(c, ct).AsTask();
        Assert.False(pendingC.IsCompleted);

        release.TrySetResult(true);
        await pendingC;

        var report = await pipeline.CompleteAndDrainAsync();
        Assert.Equal(3, report.Processed);
    }
}
