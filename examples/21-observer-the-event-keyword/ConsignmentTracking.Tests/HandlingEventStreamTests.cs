using Xunit;

namespace ConsignmentTracking.Tests;

/// <summary>
/// Proves the <see cref="IObservable{T}"/> shape: a subscribed
/// <see cref="RecordingObserver"/> receives published events in order,
/// <see cref="HandlingEventStream.Complete"/> signals completion through
/// <see cref="IObserver{T}.OnCompleted"/>, and the stream is hot, a
/// subscriber that attaches after an earlier publish never sees that
/// earlier event, unlike the <see cref="ConsignmentTracker"/> event side,
/// which retains a replayable <see cref="ConsignmentTracker.History"/>.
/// </summary>
public class HandlingEventStreamTests
{
    [Fact]
    public void Subscribed_observer_receives_published_events_in_order()
    {
        using var stream = new HandlingEventStream();
        var observer = new RecordingObserver();
        using var subscription = stream.Events.Subscribe(observer);

        var received = new HandlingEvent("CNS-500", ConsignmentStatus.Received, "Hamburg", DateTimeOffset.UtcNow);
        var loaded = new HandlingEvent("CNS-500", ConsignmentStatus.Loaded, "Hamburg", DateTimeOffset.UtcNow);
        stream.Publish(received);
        stream.Publish(loaded);

        Assert.Equal([received, loaded], observer.Received);
    }

    [Fact]
    public void Complete_signals_the_observer_as_completed()
    {
        using var stream = new HandlingEventStream();
        var observer = new RecordingObserver();
        using var subscription = stream.Events.Subscribe(observer);

        stream.Complete();

        Assert.True(observer.Completed);
        Assert.Null(observer.Error);
    }

    [Fact]
    public void Subscriber_that_subscribes_after_an_earlier_publish_misses_that_event()
    {
        using var stream = new HandlingEventStream();
        var early = new HandlingEvent("CNS-600", ConsignmentStatus.Received, "Busan", DateTimeOffset.UtcNow);
        stream.Publish(early);

        var lateObserver = new RecordingObserver();
        using var subscription = stream.Events.Subscribe(lateObserver);
        var late = new HandlingEvent("CNS-600", ConsignmentStatus.InTransit, "Busan", DateTimeOffset.UtcNow);
        stream.Publish(late);

        Assert.Equal([late], lateObserver.Received);
        Assert.DoesNotContain(early, lateObserver.Received);
    }
}
