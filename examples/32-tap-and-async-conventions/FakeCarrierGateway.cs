namespace CarrierDispatch;

/// <summary>
/// An in-memory <see cref="ICarrierGateway"/> for tests and demos. It never
/// touches the network or the wall clock: every accepted timestamp comes
/// from the <see cref="DateTimeOffset"/> given to the constructor (or
/// <see cref="DateTimeOffset.UnixEpoch"/> if none is given), so a test can
/// assert on the exact receipt it gets back.
/// </summary>
public sealed class FakeCarrierGateway(DateTimeOffset acceptedAt) : ICarrierGateway
{
    public FakeCarrierGateway()
        : this(DateTimeOffset.UnixEpoch)
    {
    }

    public async Task<DispatchReceipt> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Delay(0, cancellationToken).ConfigureAwait(false);

        return new DispatchReceipt(request.ConsignmentId, ToTrackingNumber(request.ConsignmentId), acceptedAt);
    }

    public async Task<IReadOnlyList<DispatchReceipt>> DispatchBatchAsync(
        IReadOnlyList<DispatchRequest> requests,
        IProgress<DispatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        cancellationToken.ThrowIfCancellationRequested();

        var receipts = new List<DispatchReceipt>(requests.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = requests[i];
            await Task.Delay(0, cancellationToken).ConfigureAwait(false);
            receipts.Add(new DispatchReceipt(request.ConsignmentId, ToTrackingNumber(request.ConsignmentId), acceptedAt));

            progress?.Report(new DispatchProgress(i + 1, requests.Count));
        }

        return receipts;
    }

    public ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // No actual I/O to await: a completed ValueTask<bool> reports the
        // result without allocating a Task<bool> for work that never went
        // asynchronous in the first place.
        return new ValueTask<bool>(true);
    }

    private static string ToTrackingNumber(string consignmentId) => $"TRK-{consignmentId}";
}
