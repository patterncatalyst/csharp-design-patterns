using Xunit;

namespace HandlingEventIngest.Tests;

/// <summary>
/// Proves the Producer-Consumer guarantees a <see cref="HandlingEventPipeline"/>
/// is built around: every submitted event is consumed exactly once regardless
/// of how many producers and consumers race over the channel, a single
/// producer's submission order survives a single consumer, and completion is
/// signalled through <see cref="HandlingEventPipeline.CompleteAndDrainAsync"/>
/// rather than any wall-clock wait. No <c>Thread.Sleep</c>, <c>Task.Delay</c>,
/// or timeout-based <c>Task.WhenAny</c> appears anywhere in this example;
/// every assertion here is driven by known counts and known sequences.
/// </summary>
public class PipelineTests
{
    [Fact]
    public async Task Every_event_produced_is_consumed_exactly_once()
    {
        const int ProducerCount = 4;
        const int EventsPerProducer = 250;
        var ct = TestContext.Current.CancellationToken;

        var pipeline = new HandlingEventPipeline(capacity: 8, consumerCount: 4);

        var produced = new List<HandlingEvent>();
        for (var p = 0; p < ProducerCount; p++)
        {
            var tracking = new TrackingId($"P{p}");
            for (var s = 0; s < EventsPerProducer; s++)
            {
                produced.Add(new HandlingEvent(tracking, (HandlingKind)(s % 5), s));
            }
        }

        var producers = new Task[ProducerCount];
        for (var p = 0; p < ProducerCount; p++)
        {
            var tracking = new TrackingId($"P{p}");
            producers[p] = Task.Run(async () =>
            {
                for (var s = 0; s < EventsPerProducer; s++)
                {
                    await pipeline.SubmitAsync(new HandlingEvent(tracking, (HandlingKind)(s % 5), s), ct);
                }
            }, ct);
        }

        await Task.WhenAll(producers);
        var report = await pipeline.CompleteAndDrainAsync();

        Assert.Equal(ProducerCount * EventsPerProducer, report.Processed);

        var expected = produced
            .OrderBy(e => e.Tracking.Value, StringComparer.Ordinal)
            .ThenBy(e => e.Sequence)
            .ToArray();
        var actual = report.Observed
            .OrderBy(e => e.Tracking.Value, StringComparer.Ordinal)
            .ThenBy(e => e.Sequence)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task A_single_producer_order_is_preserved_by_a_single_consumer()
    {
        const int EventCount = 100;
        var ct = TestContext.Current.CancellationToken;
        var tracking = new TrackingId("CN-9001");

        var pipeline = new HandlingEventPipeline(capacity: 4, consumerCount: 1);

        for (var s = 0; s < EventCount; s++)
        {
            await pipeline.SubmitAsync(new HandlingEvent(tracking, (HandlingKind)(s % 5), s), ct);
        }

        var report = await pipeline.CompleteAndDrainAsync();

        Assert.Equal(EventCount, report.Processed);
        Assert.Equal(Enumerable.Range(0, EventCount), report.Observed.Select(e => e.Sequence));
    }

    [Fact]
    public async Task Completion_is_signalled_so_consumers_stop_after_the_last_event()
    {
        const int EventCount = 60;
        var ct = TestContext.Current.CancellationToken;
        var tracking = new TrackingId("CN-9002");

        var pipeline = new HandlingEventPipeline(capacity: 8, consumerCount: 3);

        var submissions = new Task[EventCount];
        for (var s = 0; s < EventCount; s++)
        {
            var e = new HandlingEvent(tracking, (HandlingKind)(s % 5), s);
            submissions[s] = pipeline.SubmitAsync(e, ct).AsTask();
        }

        await Task.WhenAll(submissions);
        var report = await pipeline.CompleteAndDrainAsync();

        Assert.Equal(EventCount, report.Processed);
    }

    [Fact]
    public async Task A_cancelled_submit_token_is_honoured()
    {
        var pipeline = new HandlingEventPipeline(capacity: 1, consumerCount: 1);
        var evt = new HandlingEvent(new TrackingId("CN-9003"), HandlingKind.Receive, 0);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await pipeline.SubmitAsync(evt, cancellationSource.Token));

        var report = await pipeline.CompleteAndDrainAsync();
        Assert.Equal(0, report.Processed);
    }
}
