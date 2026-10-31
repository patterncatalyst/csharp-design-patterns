using System.Diagnostics.CodeAnalysis;

namespace TrackingHistory;

/// <summary>
/// Consumes an <see cref="ICarrierFeed"/>'s async stream for one consignment,
/// either to completion or until a chosen stage is reached.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "CollectAsync and CollectUntilAsync are instance methods by design: this reader is the " +
        "stable collaborator a caller holds onto, with the per-call feed and consignment id passed as " +
        "arguments, not fields; neither method happens to touch instance state, but static methods would " +
        "break the reader-as-collaborator shape this chapter is demonstrating.")]
public sealed class LiveTrackingReader
{
    public async Task<IReadOnlyList<TrackingEvent>> CollectAsync(
        ICarrierFeed feed, string consignmentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var collected = new List<TrackingEvent>();
        await foreach (var trackingEvent in feed.StreamAsync(consignmentId, cancellationToken)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            collected.Add(trackingEvent);
        }

        return collected;
    }

    public async Task<IReadOnlyList<TrackingEvent>> CollectUntilAsync(
        ICarrierFeed feed, string consignmentId, TrackingStage stop, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var collected = new List<TrackingEvent>();
        await foreach (var trackingEvent in feed.StreamAsync(consignmentId, cancellationToken)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            collected.Add(trackingEvent);
            if (trackingEvent.Stage == stop)
            {
                // Abandon the feed early: the enumerator is disposed once the
                // await foreach is left without exhausting it, so no event
                // past the stop stage is ever requested.
                break;
            }
        }

        return collected;
    }
}
