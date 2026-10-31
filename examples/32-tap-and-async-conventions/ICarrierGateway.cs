namespace CarrierDispatch;

/// <summary>
/// A carrier booking gateway, shaped entirely by the Task-based Asynchronous
/// Pattern (TAP): every asynchronous member name ends in "Async", returns a
/// <see cref="Task"/>, <see cref="Task{TResult}"/>, or <see cref="ValueTask{TResult}"/>,
/// and accepts a trailing <see cref="CancellationToken"/> so a caller can
/// cancel a dispatch without the gateway inventing its own cancellation
/// story.
/// </summary>
public interface ICarrierGateway
{
    /// <summary>
    /// Hands a single consignment to the carrier and returns the receipt it
    /// issues in response.
    /// </summary>
    Task<DispatchReceipt> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a batch of consignments in order, optionally reporting
    /// progress after each one completes.
    /// </summary>
    Task<IReadOnlyList<DispatchReceipt>> DispatchBatchAsync(
        IReadOnlyList<DispatchRequest> requests,
        IProgress<DispatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the carrier's booking endpoint is reachable. Returns a
    /// <see cref="ValueTask{TResult}"/> because a healthy gateway can often
    /// answer this synchronously, without allocating a <see cref="Task{TResult}"/>
    /// for work that never actually goes asynchronous.
    /// </summary>
    ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default);
}
