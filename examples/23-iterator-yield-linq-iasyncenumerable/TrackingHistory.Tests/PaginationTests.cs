using System.Collections;
using Xunit;

namespace TrackingHistory.Tests;

/// <summary>
/// Proves the synchronous iterator half of <see cref="ConsignmentHistory"/>:
/// <see cref="ConsignmentHistory.Paginate"/> buffers events into fixed-size
/// pages with a trailing partial page only when one is left over, validates
/// its page size the moment it is called rather than on first enumeration,
/// and <see cref="ConsignmentHistory.StagesReached"/> is plain LINQ over the
/// same buffered events.
/// </summary>
public class PaginationTests
{
    private static TrackingEvent Event(string consignmentId, TrackingStage stage) =>
        new(consignmentId, stage, DateTimeOffset.UnixEpoch, "SIN");

    [Fact]
    public void Paginate_yields_fixed_size_pages_and_a_final_partial_page()
    {
        var events = new[]
        {
            Event("CN-1", TrackingStage.Booked),
            Event("CN-1", TrackingStage.PickedUp),
            Event("CN-1", TrackingStage.InTransit),
            Event("CN-1", TrackingStage.CustomsHold),
            Event("CN-1", TrackingStage.OutForDelivery),
        };
        var history = new ConsignmentHistory(events);

        var pages = history.Paginate(2).ToList();

        Assert.Equal(3, pages.Count);
        Assert.Equal([1, 2, 3], pages.Select(p => p.PageNumber));
        Assert.Equal(2, pages[0].Events.Count);
        Assert.Equal(2, pages[1].Events.Count);
        Assert.Single(pages[2].Events);
        Assert.Equal(events[4], pages[2].Events[0]);
    }

    [Fact]
    public void Paginate_with_an_evenly_dividing_size_has_no_trailing_empty_page()
    {
        var events = new[]
        {
            Event("CN-2", TrackingStage.Booked),
            Event("CN-2", TrackingStage.PickedUp),
            Event("CN-2", TrackingStage.InTransit),
            Event("CN-2", TrackingStage.Delivered),
        };
        var history = new ConsignmentHistory(events);

        var pages = history.Paginate(2).ToList();

        Assert.Equal(2, pages.Count);
        Assert.All(pages, page => Assert.Equal(2, page.Events.Count));
    }

    [Fact]
    public void Paginate_validates_page_size_eagerly_before_enumeration()
    {
        var history = new ConsignmentHistory([Event("CN-3", TrackingStage.Booked)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => history.Paginate(0));
    }

    [Fact]
    public void Paginate_is_deferred_until_enumerated()
    {
        var spy = new CountingEventSource([Event("CN-4", TrackingStage.Booked), Event("CN-4", TrackingStage.PickedUp)]);
        var history = new ConsignmentHistory(spy);

        var pages = history.Paginate(2);

        Assert.Equal(0, spy.EnumerationCount);

        var materialized = pages.ToList();

        Assert.Equal(1, spy.EnumerationCount);
        Assert.Single(materialized);
    }

    [Fact]
    public void StagesReached_returns_distinct_stages_in_first_seen_order()
    {
        var events = new[]
        {
            Event("CN-5", TrackingStage.Booked),
            Event("CN-5", TrackingStage.PickedUp),
            Event("CN-5", TrackingStage.InTransit),
            Event("CN-5", TrackingStage.PickedUp),
            Event("CN-5", TrackingStage.Delivered),
        };
        var history = new ConsignmentHistory(events);

        var stages = history.StagesReached().ToList();

        Assert.Equal(
            [TrackingStage.Booked, TrackingStage.PickedUp, TrackingStage.InTransit, TrackingStage.Delivered],
            stages);
    }

    /// <summary>
    /// An <see cref="IReadOnlyList{T}"/> wrapper that counts how many times it
    /// has been enumerated, so a test can prove an iterator method defers its
    /// work until something actually walks it. Implementing
    /// <see cref="IReadOnlyList{T}"/> (rather than only
    /// <see cref="IEnumerable{T}"/>) matters here: it lets
    /// <see cref="ConsignmentHistory"/>'s constructor store this instance
    /// directly instead of copying it into a fresh list up front, which would
    /// enumerate it before the test ever calls <c>Paginate</c>.
    /// </summary>
    private sealed class CountingEventSource(IReadOnlyList<TrackingEvent> events) : IReadOnlyList<TrackingEvent>
    {
        public int EnumerationCount { get; private set; }

        public int Count => events.Count;

        public TrackingEvent this[int index] => events[index];

        public IEnumerator<TrackingEvent> GetEnumerator()
        {
            EnumerationCount++;
            return events.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
