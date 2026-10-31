namespace TrackingHistory;

/// <summary>
/// The buffered, already-known history of a single consignment. Everything
/// here runs synchronously over the events given to the constructor; the
/// live, in-flight counterpart is <see cref="CarrierEventFeed"/>.
/// </summary>
public sealed class ConsignmentHistory
{
    private readonly IReadOnlyList<TrackingEvent> _events;

    public ConsignmentHistory(IEnumerable<TrackingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events as IReadOnlyList<TrackingEvent> ?? [.. events];
    }

    /// <summary>
    /// Splits the history into pages of <paramref name="pageSize"/> events.
    /// This method is deliberately NOT an iterator itself: a C# iterator
    /// method defers its entire body, including argument checks, until the
    /// caller enumerates it, so an invalid <paramref name="pageSize"/> would
    /// otherwise surface only on the first <c>MoveNext</c> rather than on the
    /// call. Validating here and delegating the actual <c>yield return</c>
    /// work to <see cref="PaginateIterator"/> keeps the eager validation and
    /// the lazy pagination each in the one place they belong.
    /// </summary>
    public IEnumerable<TrackingPage> Paginate(int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize), pageSize, "Page size must be a positive number of events.");
        }

        return PaginateIterator(pageSize);
    }

    private IEnumerable<TrackingPage> PaginateIterator(int pageSize)
    {
        var pageNumber = 1;
        var buffer = new List<TrackingEvent>(pageSize);

        foreach (var trackingEvent in _events)
        {
            buffer.Add(trackingEvent);
            if (buffer.Count == pageSize)
            {
                yield return new TrackingPage(pageNumber, buffer);
                pageNumber++;
                buffer = new List<TrackingEvent>(pageSize);
            }
        }

        if (buffer.Count > 0)
        {
            yield return new TrackingPage(pageNumber, buffer);
        }
    }

    /// <summary>The distinct stages reached so far, in the order each was first seen.</summary>
    public IEnumerable<TrackingStage> StagesReached() => _events.Select(e => e.Stage).Distinct();
}
