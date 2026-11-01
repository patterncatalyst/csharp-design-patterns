namespace ConcurrentHandling;

/// <summary>
/// Validates a <see cref="HandlingEvent"/> against the manifest before it is
/// recorded. Returns a <see cref="Task{TResult}"/> so a decorator can await an
/// inner validator that performs genuine asynchronous work.
/// </summary>
public interface IManifestValidator
{
    Task<bool> ValidateAsync(HandlingEvent e, CancellationToken ct);
}
