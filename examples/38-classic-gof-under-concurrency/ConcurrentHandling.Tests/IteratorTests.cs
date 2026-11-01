using Xunit;

namespace ConcurrentHandling.Tests;

/// <summary>
/// Proves <see cref="HandlingEventFeed"/> behaves like the Iterator pattern
/// over a stream that is still being written: events arrive in publish order,
/// a cancelled token stops the traversal, and a producer and a consumer can
/// run concurrently over the same feed without losing an event.
/// </summary>
public class IteratorTests
{
    private const int EventCount = 25;

    [Fact]
    public async Task The_feed_streams_every_published_event_in_order()
    {
        var feed = new HandlingEventFeed();
        var tracking = new TrackingId("C-ITER-1");
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < EventCount; i++)
        {
            await feed.PublishAsync(new HandlingEvent(tracking, HandlingKind.Load, i), ct);
        }

        feed.Complete();

        var received = new List<HandlingEvent>();
        await foreach (var e in feed.StreamAsync(ct))
        {
            received.Add(e);
        }

        Assert.Equal(Enumerable.Range(0, EventCount), received.Select(e => e.Sequence));
    }

    [Fact]
    public async Task A_cancelled_stream_token_is_honoured()
    {
        var feed = new HandlingEventFeed();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in feed.StreamAsync(cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task A_producer_and_a_consumer_run_concurrently_over_the_feed()
    {
        var feed = new HandlingEventFeed();
        var tracking = new TrackingId("C-ITER-3");
        var ct = TestContext.Current.CancellationToken;
        var consumedCount = 0;

        var producer = Task.Run(async () =>
        {
            for (var i = 0; i < EventCount; i++)
            {
                await feed.PublishAsync(new HandlingEvent(tracking, HandlingKind.Unload, i), ct);
            }

            feed.Complete();
        }, ct);

        var consumer = Task.Run(async () =>
        {
            await foreach (var _ in feed.StreamAsync(ct))
            {
                Interlocked.Increment(ref consumedCount);
            }
        }, ct);

        await Task.WhenAll(producer, consumer);

        Assert.Equal(EventCount, consumedCount);
    }
}
