using Xunit;

namespace ConcurrentHandling.Tests;

/// <summary>
/// Proves a <see cref="RecordHandlingCommand"/> records its event and returns
/// a matching receipt, and that <see cref="CommandInvoker"/> runs a batch of
/// commands concurrently while still handing back one receipt per command
/// with distinct, gap-free sequence numbers.
/// </summary>
public class CommandTests
{
    private const int CommandCount = 20;

    [Fact]
    public async Task A_handling_command_records_its_event_and_returns_a_receipt()
    {
        var ledger = new InMemoryHandlingLedger();
        var tracking = new TrackingId("C-CMD-1");
        var evt = new HandlingEvent(tracking, HandlingKind.Receive, 0);
        var command = new RecordHandlingCommand(evt, ledger);
        var ct = TestContext.Current.CancellationToken;

        var receipt = await command.ExecuteAsync(ct);

        Assert.Equal(tracking, receipt.Tracking);
        Assert.True(receipt.Sequence >= 1);
        Assert.Contains(evt, ledger.Entries);
    }

    [Fact]
    public async Task The_invoker_runs_every_command_and_returns_a_receipt_each()
    {
        var ledger = new InMemoryHandlingLedger();
        var tracking = new TrackingId("C-CMD-2");
        var ct = TestContext.Current.CancellationToken;
        var commands = Enumerable.Range(0, CommandCount)
            .Select(i => (IHandlingCommand)new RecordHandlingCommand(
                new HandlingEvent(tracking, HandlingKind.Load, i), ledger))
            .ToList();
        var invoker = new CommandInvoker();

        var receipts = await invoker.RunAllAsync(commands, ct);

        Assert.Equal(CommandCount, receipts.Count);
        var sequences = receipts.Select(r => r.Sequence).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(1, CommandCount), sequences);
    }
}
