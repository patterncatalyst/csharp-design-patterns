using NSubstitute;
using Xunit;

namespace BookingAmendments.Tests;

/// <summary>
/// Proves the Task-returning counterpart of the queue: <see cref="AsyncAmendmentQueue.RunAsync"/>
/// awaits each command's gateway call in order, <see cref="AsyncAmendmentQueue.UndoAsync"/>
/// reverses the most recently executed one, and the caller's cancellation
/// token reaches the gateway rather than being replaced along the way.
/// </summary>
public class AsyncAmendmentQueueTests
{
    [Fact]
    public async Task RunAsync_awaits_each_command_through_the_gateway_in_fifo_order()
    {
        var gateway = Substitute.For<IAmendmentGateway>();
        var queue = new AsyncAmendmentQueue();
        queue.Enqueue(new ConfirmAmendmentCommand(gateway, "BK-6001"));
        queue.Enqueue(new ConfirmAmendmentCommand(gateway, "BK-6002"));

        await queue.RunAsync(TestContext.Current.CancellationToken);

        await gateway.Received(1).ConfirmAsync("BK-6001", Arg.Any<CancellationToken>());
        await gateway.Received(1).ConfirmAsync("BK-6002", Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            gateway.ConfirmAsync("BK-6001", Arg.Any<CancellationToken>());
            gateway.ConfirmAsync("BK-6002", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task UndoAsync_reverses_the_most_recently_executed_command()
    {
        var gateway = Substitute.For<IAmendmentGateway>();
        var queue = new AsyncAmendmentQueue();
        queue.Enqueue(new ConfirmAmendmentCommand(gateway, "BK-6003"));
        queue.Enqueue(new ConfirmAmendmentCommand(gateway, "BK-6004"));
        await queue.RunAsync(TestContext.Current.CancellationToken);

        await queue.UndoAsync(TestContext.Current.CancellationToken);

        await gateway.Received(1).RevertAsync("BK-6004", Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().RevertAsync("BK-6003", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UndoAsync_with_no_executed_history_throws()
    {
        var queue = new AsyncAmendmentQueue();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.UndoAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Threads_the_callers_cancellation_token_through_confirm_and_revert()
    {
        var gateway = Substitute.For<IAmendmentGateway>();
        var queue = new AsyncAmendmentQueue();
        using var cancellationSource = new CancellationTokenSource();
        queue.Enqueue(new ConfirmAmendmentCommand(gateway, "BK-6005"));

        await queue.RunAsync(cancellationSource.Token);
        await queue.UndoAsync(cancellationSource.Token);

        await gateway.Received(1).ConfirmAsync("BK-6005", cancellationSource.Token);
        await gateway.Received(1).RevertAsync("BK-6005", cancellationSource.Token);
    }
}
