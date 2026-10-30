using System.Diagnostics.CodeAnalysis;

namespace BookingAmendments;

/// <summary>
/// Queues amendment commands and runs them first-in-first-out, then lets a
/// caller step backward through the commands that actually executed, last
/// executed first.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "AmendmentQueue is a Command-pattern invoker backed internally by a Queue<T>, " +
        "not an implementation of the Queue<T> contract itself; the name is fixed for this chapter.")]
public sealed class AmendmentQueue
{
    private readonly Queue<IBookingCommand> _pending = new();
    private readonly Stack<IBookingCommand> _history = new();
    private readonly Stack<IBookingCommand> _undone = new();

    public void Enqueue(IBookingCommand command) => _pending.Enqueue(command);

    /// <summary>
    /// Executes every queued command in the order it was enqueued, pushing
    /// each onto the undo history as it runs. Running clears anything
    /// previously undone, since the commands this queue would redo may no
    /// longer apply against a booking that has since moved on.
    /// </summary>
    public void Run()
    {
        _undone.Clear();

        while (_pending.TryDequeue(out var command))
        {
            command.Execute();
            _history.Push(command);
        }
    }

    /// <summary>
    /// Reverses the most recently executed command that has not already been
    /// undone.
    /// </summary>
    public void Undo()
    {
        if (!_history.TryPop(out var command))
        {
            throw new InvalidOperationException("No executed command to undo.");
        }

        command.Undo();
        _undone.Push(command);
    }

    /// <summary>
    /// Re-executes the most recently undone command.
    /// </summary>
    public void Redo()
    {
        if (!_undone.TryPop(out var command))
        {
            throw new InvalidOperationException("No undone command to redo.");
        }

        command.Execute();
        _history.Push(command);
    }
}
