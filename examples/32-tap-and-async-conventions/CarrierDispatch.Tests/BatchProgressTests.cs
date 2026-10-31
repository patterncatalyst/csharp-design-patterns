using Xunit;

namespace CarrierDispatch.Tests;

/// <summary>
/// Proves <see cref="FakeCarrierGateway.DispatchBatchAsync"/> processes a
/// batch in order, reports progress through a synchronous
/// <see cref="RecordingProgress"/> double after each item, tolerates a null
/// progress callback, and honours cancellation requested partway through.
/// </summary>
public class BatchProgressTests
{
    private static IReadOnlyList<DispatchRequest> ThreeRequests() =>
    [
        new DispatchRequest("CN-8001", "Rotterdam", 100m),
        new DispatchRequest("CN-8002", "Hamburg", 200m),
        new DispatchRequest("CN-8003", "Antwerp", 300m),
    ];

    [Fact]
    public async Task DispatchBatchAsync_reports_progress_for_each_item()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var requests = ThreeRequests();
        var progress = new RecordingProgress();

        await gateway.DispatchBatchAsync(requests, progress, TestContext.Current.CancellationToken);

        Assert.Equal(requests.Count, progress.Reports.Count);
        var finalReport = progress.Reports[^1];
        Assert.Equal(finalReport.Total, finalReport.Completed);
    }

    [Fact]
    public async Task DispatchBatchAsync_without_a_progress_callback_still_completes()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var requests = ThreeRequests();

        var receipts = await gateway.DispatchBatchAsync(requests, progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(requests.Count, receipts.Count);
    }

    [Fact]
    public async Task DispatchBatchAsync_honours_cancellation_mid_batch()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var requests = ThreeRequests();
        using var cancellationSource = new CancellationTokenSource();
        var progress = new CancelOnFirstReportProgress(cancellationSource);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gateway.DispatchBatchAsync(requests, progress, cancellationSource.Token));

        Assert.True(progress.Reports.Count < requests.Count);
    }

    [Fact]
    public async Task Progress_reports_are_monotonic()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var requests = ThreeRequests();
        var progress = new RecordingProgress();

        await gateway.DispatchBatchAsync(requests, progress, TestContext.Current.CancellationToken);

        for (var i = 0; i < progress.Reports.Count; i++)
        {
            Assert.Equal(i + 1, progress.Reports[i].Completed);
        }
    }

    [Fact]
    public async Task DispatchBatchAsync_returns_a_receipt_per_request()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var requests = ThreeRequests();

        var receipts = await gateway.DispatchBatchAsync(requests, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(requests.Count, receipts.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            Assert.Equal(requests[i].ConsignmentId, receipts[i].ConsignmentId);
        }
    }

    /// <summary>
    /// A synchronous progress double that cancels its source as soon as it
    /// records the first report, so the next loop iteration's
    /// <see cref="CancellationToken.ThrowIfCancellationRequested"/> check
    /// stops the batch before every request is dispatched.
    /// </summary>
    private sealed class CancelOnFirstReportProgress(CancellationTokenSource cancellationSource) : IProgress<DispatchProgress>
    {
        public List<DispatchProgress> Reports { get; } = [];

        public void Report(DispatchProgress value)
        {
            Reports.Add(value);
            cancellationSource.Cancel();
        }
    }
}
