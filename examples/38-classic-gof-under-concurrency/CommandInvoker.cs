namespace ConcurrentHandling;

/// <summary>
/// The Command pattern's invoker, running a batch of commands concurrently
/// rather than one at a time. Each command starts immediately, and
/// <see cref="Task.WhenAll{TResult}(IEnumerable{Task{TResult}})"/> gathers
/// every result (or propagates the first exception) once all of them finish.
/// <see cref="TotalCommandsRun"/> is a running count of commands this invoker
/// has completed across every call to <see cref="RunAllAsync"/>, the one
/// piece of instance state an invoker naturally keeps.
/// </summary>
public sealed class CommandInvoker
{
    private int _totalCommandsRun;

    public int TotalCommandsRun => Volatile.Read(ref _totalCommandsRun);

    public async Task<IReadOnlyList<HandlingReceipt>> RunAllAsync(
        IReadOnlyCollection<IHandlingCommand> commands, CancellationToken ct)
    {
        var pending = commands.Select(c => c.ExecuteAsync(ct)).ToList();
        var receipts = await Task.WhenAll(pending);
        Interlocked.Add(ref _totalCommandsRun, receipts.Length);
        return receipts;
    }
}
