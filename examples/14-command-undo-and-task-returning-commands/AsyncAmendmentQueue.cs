using System.Diagnostics.CodeAnalysis;

namespace BookingAmendments;

/// <summary>
/// The Task-returning counterpart to <see cref="AmendmentQueue"/>: awaits
/// each queued command's <see cref="IAsyncBookingCommand.ExecuteAsync"/> in
/// order, and reverses the most recently executed one on
/// <see cref="UndoAsync"/>. Every await is configured not to resume on the
/// captured context, and the caller's cancellation token is threaded through
/// to both the command and, transitively, its gateway call.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "AsyncAmendmentQueue is a Command-pattern invoker backed internally by a Queue<T>, " +
        "not an implementation of the Queue<T> contract itself; the name is fixed for this chapter.")]
public sealed class AsyncAmendmentQueue
{
    private readonly Queue<IAsyncBookingCommand> _pending = new();
    private readonly Stack<IAsyncBookingCommand> _history = new();

    public void Enqueue(IAsyncBookingCommand command) => _pending.Enqueue(command);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (_pending.TryDequeue(out var command))
        {
            await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            _history.Push(command);
        }
    }

    public async Task UndoAsync(CancellationToken cancellationToken)
    {
        if (!_history.TryPop(out var command))
        {
            throw new InvalidOperationException("No executed command to undo.");
        }

        await command.UndoAsync(cancellationToken).ConfigureAwait(false);
    }
}
