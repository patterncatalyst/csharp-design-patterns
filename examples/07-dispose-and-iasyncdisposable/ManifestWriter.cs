using System.Text;

namespace ManifestWriting;

/// <summary>
/// Appends consignment manifest lines to a file, synchronously. Owns the
/// underlying <see cref="FileStream"/> handle: a warehouse worker who opens
/// this same manifest file in another tool while the writer is still active
/// must be refused (the writer asks for <see cref="FileShare.None"/>), and
/// that same worker must succeed the instant the writer is done, not
/// whenever the garbage collector eventually finalizes some abandoned
/// instance.
/// </summary>
public sealed class ManifestWriter : IDisposable
{
    private readonly FileStream _stream;
    private bool _disposed;

    public ManifestWriter(string path)
    {
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    /// <summary>
    /// Appends one manifest entry as a line of text and flushes it to disk
    /// immediately, so a reader opening the file after this call returns
    /// sees the entry without waiting on a buffered write to land.
    /// </summary>
    public void WriteLine(ManifestEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var line = $"{entry.ShipmentId},{entry.Destination},{entry.WeightKilograms}{Environment.NewLine}";
        var bytes = Encoding.UTF8.GetBytes(line);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Flush();
    }

    // No finalizer and no Dispose(bool) overload here. The full dispose
    // pattern with a finalizer exists so that a *subclassable* type can
    // still release unmanaged resources if a caller forgets to call
    // Dispose. ManifestWriter is sealed (no subclass can add an unmanaged
    // resource underneath it) and owns only one managed IDisposable, the
    // FileStream, which already has its own finalizer as a backstop. Adding
    // a finalizer here would just slow down collection for no safety gain,
    // which is exactly what CA1063 and CA1816 flag.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _stream.Dispose();
        _disposed = true;
    }
}
