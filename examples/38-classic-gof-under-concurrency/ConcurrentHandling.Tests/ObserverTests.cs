using Xunit;

namespace ConcurrentHandling.Tests;

/// <summary>
/// Proves <see cref="HandlingEventHub"/> delivers every raised event to every
/// subscriber, including when raises and subscription changes happen
/// concurrently, because <see cref="Raise"/> iterates an immutable snapshot
/// rather than a live, lock-guarded list.
/// </summary>
public class ObserverTests
{
    private const int ObserverCount = 5;
    private const int EventCount = 7;
    private const int RaiserCount = 32;

    [Fact]
    public void Every_subscribed_observer_sees_every_raised_event()
    {
        var hub = new HandlingEventHub();
        var observers = Enumerable.Range(0, ObserverCount).Select(_ => new CountingObserver()).ToList();
        foreach (var observer in observers)
        {
            hub.Subscribe(observer);
        }

        var tracking = new TrackingId("C-OBS-1");
        var produced = Enumerable.Range(0, EventCount)
            .Select(i => new HandlingEvent(tracking, HandlingKind.Load, i))
            .ToList();

        foreach (var e in produced)
        {
            hub.Raise(e);
        }

        Assert.All(observers, observer =>
        {
            Assert.Equal(EventCount, observer.Count);
            Assert.Equal(produced, observer.Received);
        });
    }

    [Fact]
    public async Task Raising_across_threads_delivers_every_event_exactly_once()
    {
        var hub = new HandlingEventHub();
        var observer = new CountingObserver();
        hub.Subscribe(observer);
        using var barrier = new Barrier(RaiserCount);
        var tracking = new TrackingId("C-OBS-2");
        var produced = Enumerable.Range(0, RaiserCount)
            .Select(i => new HandlingEvent(tracking, HandlingKind.Receive, i))
            .ToList();

        var raisers = produced.Select(e => Task.Run(() =>
        {
            barrier.SignalAndWait();
            hub.Raise(e);
        }));

        await Task.WhenAll(raisers);

        Assert.Equal(RaiserCount, observer.Count);
        Assert.Equal(
            produced.OrderBy(e => e.Sequence).ToList(),
            observer.Received.OrderBy(e => e.Sequence).ToList());
    }

    [Fact]
    public void Unsubscribing_during_a_raise_leaves_the_in_flight_snapshot_intact()
    {
        var hub = new HandlingEventHub();
        var a = new UnsubscribingObserver();
        var b = new UnsubscribingObserver();

        hub.Subscribe(a);
        var subscriptionB = hub.Subscribe(b);
        a.SubscriptionToDispose = subscriptionB;

        var tracking = new TrackingId("C-OBS-3");
        var first = new HandlingEvent(tracking, HandlingKind.Customs, 0);

        var exception = Record.Exception(() => hub.Raise(first));

        Assert.Null(exception);
        Assert.Equal(1, a.NotifiedCount);
        Assert.Equal(1, b.NotifiedCount);

        var second = new HandlingEvent(tracking, HandlingKind.Claim, 1);
        hub.Raise(second);

        Assert.Equal(2, a.NotifiedCount);
        Assert.Equal(1, b.NotifiedCount);
    }
}
