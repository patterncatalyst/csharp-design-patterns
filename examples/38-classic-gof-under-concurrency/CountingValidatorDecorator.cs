namespace ConcurrentHandling;

/// <summary>
/// Decorates an <see cref="IManifestValidator"/> with a call counter, the
/// asynchronous shape of the classic Decorator pattern: the wrapper
/// implements the same interface as the component it wraps and forwards every
/// call to it, adding behavior (here, counting) before or after the
/// delegation. The counter is updated with <see cref="Interlocked.Increment(ref int)"/>
/// because many callers can be awaiting <see cref="ValidateAsync"/>
/// concurrently.
/// </summary>
public sealed class CountingValidatorDecorator(IManifestValidator inner) : IManifestValidator
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public async Task<bool> ValidateAsync(HandlingEvent e, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        return await inner.ValidateAsync(e, ct);
    }
}
