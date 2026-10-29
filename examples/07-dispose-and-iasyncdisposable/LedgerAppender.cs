namespace ManifestWriting;

/// <summary>
/// Appends booking-ledger lines asynchronously. Owns an async-capable
/// <see cref="StreamWriter"/> over a <see cref="FileStream"/> opened for
/// exclusive access, and releases it deterministically on
/// <see cref="DisposeAsync"/>: the ledger file is unlocked and its contents
/// are flushed to disk the instant an <c>await using</c> scope ends, not
/// whenever a later GC happens to run the finalizer.
/// </summary>
public sealed class LedgerAppender : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed;

    public LedgerAppender(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        _writer = new StreamWriter(stream);
    }

    /// <summary>
    /// Appends one ledger line and flushes it, so the entry is durable on
    /// disk before this call returns rather than sitting in a buffer until
    /// the writer happens to fill up.
    /// </summary>
    public async Task AppendAsync(string line, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes and closes the underlying stream. Disposing a
    /// <see cref="StreamWriter"/> asynchronously flushes it first, so the
    /// ledger file is both fully written and unlocked by the time this
    /// method completes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _writer.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
