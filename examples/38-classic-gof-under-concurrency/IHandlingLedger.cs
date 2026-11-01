namespace ConcurrentHandling;

/// <summary>
/// Durably records a <see cref="HandlingEvent"/> and assigns it a sequence
/// number. The receiver a <see cref="IHandlingCommand"/> delegates to.
/// </summary>
public interface IHandlingLedger
{
    Task<int> AppendAsync(HandlingEvent e, CancellationToken ct);
}
