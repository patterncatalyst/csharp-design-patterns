namespace ConcurrentHandling;

/// <summary>
/// A handling request packaged as an object, the classic Command pattern. The
/// asynchronous form returns a <see cref="Task{TResult}"/> rather than
/// <c>void Execute()</c>, so a <see cref="CommandInvoker"/> can run many
/// commands concurrently and await all of their results together.
/// </summary>
public interface IHandlingCommand
{
    Task<HandlingReceipt> ExecuteAsync(CancellationToken ct);
}
