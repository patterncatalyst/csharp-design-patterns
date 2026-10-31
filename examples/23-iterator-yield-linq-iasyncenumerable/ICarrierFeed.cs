namespace TrackingHistory;

/// <summary>A carrier's live tracking feed for a single consignment, as an async stream.</summary>
public interface ICarrierFeed
{
    IAsyncEnumerable<TrackingEvent> StreamAsync(string consignmentId, CancellationToken cancellationToken);
}
