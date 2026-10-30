using Xunit;

namespace ConsignmentTracking.Tests;

/// <summary>
/// Proves the multicast event shape: every attached handler receives every
/// reported handling event, detaching one handler stops delivery to it
/// without disturbing the other, the tracker keeps its own history
/// independent of who is listening, and raising the event with zero
/// subscribers attached is safe.
/// </summary>
public class ConsignmentTrackerTests
{
    [Fact]
    public void Both_subscribers_receive_each_reported_event_in_order()
    {
        var tracker = new ConsignmentTracker();
        var notifier = new CustomerNotifier();
        var auditLog = new AuditLog();
        tracker.HandlingReported += notifier.OnHandlingReported;
        tracker.HandlingReported += auditLog.OnHandlingReported;

        var received = new HandlingEvent("CNS-100", ConsignmentStatus.Received, "Rotterdam", DateTimeOffset.UtcNow);
        var loaded = new HandlingEvent("CNS-100", ConsignmentStatus.Loaded, "Rotterdam", DateTimeOffset.UtcNow);
        tracker.Report(received);
        tracker.Report(loaded);

        Assert.Equal([received, loaded], notifier.Notifications);
        Assert.Equal([received, loaded], auditLog.Entries);
    }

    [Fact]
    public void Unsubscribing_one_handler_stops_delivery_to_it_while_the_other_still_receives()
    {
        var tracker = new ConsignmentTracker();
        var notifier = new CustomerNotifier();
        var auditLog = new AuditLog();
        tracker.HandlingReported += notifier.OnHandlingReported;
        tracker.HandlingReported += auditLog.OnHandlingReported;

        var received = new HandlingEvent("CNS-200", ConsignmentStatus.Received, "Felixstowe", DateTimeOffset.UtcNow);
        tracker.Report(received);

        tracker.HandlingReported -= notifier.OnHandlingReported;
        var inTransit = new HandlingEvent("CNS-200", ConsignmentStatus.InTransit, "Felixstowe", DateTimeOffset.UtcNow);
        tracker.Report(inTransit);

        Assert.Equal([received], notifier.Notifications);
        Assert.Equal([received, inTransit], auditLog.Entries);
    }

    [Fact]
    public void Report_records_the_event_in_history()
    {
        var tracker = new ConsignmentTracker();
        var customsHold = new HandlingEvent("CNS-300", ConsignmentStatus.CustomsHold, "Singapore", DateTimeOffset.UtcNow);

        tracker.Report(customsHold);

        Assert.Equal([customsHold], tracker.History);
    }

    [Fact]
    public void Report_with_no_subscribers_does_not_throw()
    {
        var tracker = new ConsignmentTracker();
        var delivered = new HandlingEvent("CNS-400", ConsignmentStatus.Delivered, "Santos", DateTimeOffset.UtcNow);

        var exception = Record.Exception(() => tracker.Report(delivered));

        Assert.Null(exception);
        Assert.Equal([delivered], tracker.History);
    }
}
