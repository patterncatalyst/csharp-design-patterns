using System.Runtime.CompilerServices;

namespace TrackingHistory;

/// <summary>
/// An <see cref="ICarrierFeed"/> backed by an in-memory sequence of events,
/// standing in for a carrier's live tracking API. Each matching event is
/// handed back through an `async IAsyncEnumerable&lt;T&gt;` iterator: the
/// <see cref="EnumeratorCancellationAttribute"/> on the token parameter is
/// what lets a caller's <c>.WithCancellation(token)</c> reach this method's
/// own <paramref name="cancellationToken"/> parameter rather than the
/// default, uncancellable token the compiler would otherwise generate.
/// </summary>
public sealed class CarrierEventFeed(IEnumerable<TrackingEvent> events) : ICarrierFeed
{
    private readonly IReadOnlyList<TrackingEvent> _events = events as IReadOnlyList<TrackingEvent> ?? [.. events];

    public async IAsyncEnumerable<TrackingEvent> StreamAsync(
        string consignmentId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var trackingEvent in _events)
        {
            if (trackingEvent.ConsignmentId != consignmentId)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Stand in for the network hop an actual carrier call would make,
            // without any wall-clock delay that would make the test suite flaky.
            await Task.Yield();

            yield return trackingEvent;
        }
    }
}
