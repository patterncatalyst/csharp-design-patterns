using System.Threading.Channels;

namespace StowageCoordination;

/// <summary>
/// STAGE 4 - Active Object on a bounded <see cref="Channel{T}"/>. The gate is
/// gone entirely; the queue itself is the synchronization, and the servant
/// goes back to being plain, single-threaded code because only one consumer
/// ever touches it. POSA2's six Active Object participants map onto this
/// type as: Proxy is <see cref="ReserveAsync"/>, Method Request is the
/// private <c>ReserveRequest</c> record, Activation List is the bounded
/// channel, Scheduler is the <c>await foreach</c> loop in
/// <see cref="RunAsync"/>, Servant is <see cref="StowagePlan"/>, and Future
/// is the <see cref="Task{TResult}"/> handed back through a
/// <see cref="TaskCompletionSource{TResult}"/>. Future and Scheduler never
/// appear as named types written by hand; they dissolved into the language's
/// own <c>Task</c> and <c>await foreach</c>.
/// </summary>
public sealed class StowageActiveObject : IAsyncDisposable
{
    private sealed record ReserveRequest(
        ConsignmentId Consignment,
        SlotRequirements Requirements,
        TaskCompletionSource<SlotAssignment?> Completion,
        CancellationToken Ct);

    private readonly StowagePlan _servant;
    private readonly IStowageLedger _ledger;
    private readonly Channel<ReserveRequest> _channel;
    private readonly Task _pump;

    public StowageActiveObject(StowagePlan servant, IStowageLedger ledger, int capacity)
    {
        _servant = servant;
        _ledger = ledger;
        _channel = Channel.CreateBounded<ReserveRequest>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });
        _pump = Task.Run(RunAsync);
    }

    /// <summary>
    /// The Proxy. Writing a request to a full bounded channel awaits, and
    /// that await is the backpressure: a caller is suspended until the
    /// scheduler drains room for one more request.
    /// </summary>
    public async Task<SlotAssignment?> ReserveAsync(
        ConsignmentId id, SlotRequirements req, CancellationToken ct = default) =>
        await await EnqueueAsync(id, req, ct);

    /// <summary>
    /// Test seam: splits enqueueing (which completes once the write lands in
    /// the channel) from awaiting the reservation's own result, so a test can
    /// observe a write blocking on a full channel without a wall-clock
    /// timeout.
    /// </summary>
    internal async Task<Task<SlotAssignment?>> EnqueueAsync(
        ConsignmentId id, SlotRequirements req, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<SlotAssignment?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await _channel.Writer.WriteAsync(new ReserveRequest(id, req, tcs, ct), ct);
        return tcs.Task;
    }

    /// <summary>
    /// The Scheduler. Exactly one consumer drains the channel, so the servant
    /// is never touched by more than one caller at a time.
    /// </summary>
    private async Task RunAsync()
    {
        await foreach (var req in _channel.Reader.ReadAllAsync())
        {
            if (req.Ct.IsCancellationRequested)
            {
                req.Completion.TrySetCanceled(req.Ct);
                continue;
            }

            try
            {
                var assignment = _servant.TryReserve(req.Consignment, req.Requirements);
                if (assignment is not null)
                    await _ledger.AppendAsync(assignment, req.Ct);
                req.Completion.TrySetResult(assignment);
            }
            catch (OperationCanceledException)
            {
                req.Completion.TrySetCanceled(req.Ct);
            }
            catch (Exception ex)
            {
                req.Completion.TrySetException(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _pump;
    }
}
