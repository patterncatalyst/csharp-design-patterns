namespace ConcurrentHandling;

/// <summary>
/// The concrete component being decorated: a manifest check that yields back
/// to the scheduler with <see cref="Task.Yield"/> to stand in for an
/// asynchronous lookup (a manifest service call, a database read) without
/// depending on wall-clock time.
/// </summary>
public sealed class ManifestValidator : IManifestValidator
{
    public async Task<bool> ValidateAsync(HandlingEvent e, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        return e.Sequence >= 0;
    }
}
