using Xunit;

namespace CarrierDispatch.Tests;

/// <summary>
/// Proves <see cref="FakeCarrierGateway.DispatchAsync"/> returns a receipt
/// for the request it was given, and that it honours a cancellation token
/// handed to it from the outside rather than running to completion anyway.
/// </summary>
public class DispatchAsyncTests
{
    [Fact]
    public async Task DispatchAsync_returns_a_receipt_for_the_request()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var request = new DispatchRequest("CN-7001", "Rotterdam", 182.5m);

        var receipt = await gateway.DispatchAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("CN-7001", receipt.ConsignmentId);
        Assert.Equal(DateTimeOffset.UnixEpoch, receipt.AcceptedAt);
        Assert.False(string.IsNullOrWhiteSpace(receipt.TrackingNumber));
    }

    [Fact]
    public async Task DispatchAsync_honours_a_cancelled_token()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);
        var request = new DispatchRequest("CN-7002", "Rotterdam", 182.5m);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gateway.DispatchAsync(request, cancellationSource.Token));
    }
}
