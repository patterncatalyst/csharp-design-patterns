using Xunit;

namespace TrackingHistory.Tests;

/// <summary>
/// Proves the async-stream half: <see cref="CarrierEventFeed"/> yields its
/// matching events one at a time through <see cref="IAsyncEnumerable{T}"/>,
/// <see cref="LiveTrackingReader.CollectUntilAsync"/> abandons the feed as
/// soon as it sees the stage it is waiting for, and an already-cancelled
/// token stops the stream before it yields anything.
/// </summary>
public class AsyncStreamTests
{
    private static TrackingEvent Event(string consignmentId, TrackingStage stage) =>
        new(consignmentId, stage, DateTimeOffset.UnixEpoch, "SIN");

    [Fact]
    public async Task StreamAsync_yields_every_event_in_order()
    {
        var events = new[]
        {
            Event("CN-10", TrackingStage.Booked),
            Event("CN-10", TrackingStage.PickedUp),
            Event("CN-10", TrackingStage.InTransit),
            Event("CN-10", TrackingStage.Delivered),
        };
        var feed = new CarrierEventFeed(events);
        var reader = new LiveTrackingReader();

        var collected = await reader.CollectAsync(feed, "CN-10", TestContext.Current.CancellationToken);

        Assert.Equal(events, collected);
    }

    [Fact]
    public async Task CollectUntilAsync_stops_the_feed_early_on_break()
    {
        var events = new[]
        {
            Event("CN-11", TrackingStage.Booked),
            Event("CN-11", TrackingStage.PickedUp),
            Event("CN-11", TrackingStage.InTransit),
            Event("CN-11", TrackingStage.CustomsHold),
            Event("CN-11", TrackingStage.OutForDelivery),
            Event("CN-11", TrackingStage.Delivered),
        };
        var feed = new CarrierEventFeed(events);
        var reader = new LiveTrackingReader();

        var collected = await reader.CollectUntilAsync(
            feed, "CN-11", TrackingStage.InTransit, TestContext.Current.CancellationToken);

        Assert.Equal(3, collected.Count);
        Assert.Equal(TrackingStage.InTransit, collected[^1].Stage);
    }

    [Fact]
    public async Task StreamAsync_honours_cancellation()
    {
        var feed = new CarrierEventFeed([Event("CN-12", TrackingStage.Booked)]);
        var reader = new LiveTrackingReader();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.CollectAsync(feed, "CN-12", cancellationSource.Token));
    }
}
