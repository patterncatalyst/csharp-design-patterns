using System.Buffers;
using System.Text;

namespace GatewayPooling;

/// <summary>
/// Serializes <see cref="ManifestLine"/> records into the on-the-wire EDI
/// manifest format, <c>Code=Value\n</c> per line, UTF-8 encoded. Encoding
/// borrows a scratch buffer from an <see cref="ArrayPool{T}"/> rather than
/// allocating one per call: the upper bound on bytes needed is computed from
/// the input first, a buffer at least that large is rented, the encoding
/// happens in place, and only the final trimmed copy is allocated and
/// returned to the caller.
/// </summary>
public sealed class ManifestWriter
{
    private readonly ArrayPool<byte> _pool;

    public ManifestWriter(ArrayPool<byte> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
    }

    /// <summary>
    /// Encodes <paramref name="lines"/> into the wire format and returns the
    /// exact-length encoded bytes. Identical input always computes the same
    /// upper bound, so repeated calls rent from the same ArrayPool bucket and
    /// can observe buffer reuse.
    /// </summary>
    public byte[] Serialize(IReadOnlyList<ManifestLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var upperBound = UpperBoundBytes(lines);
        var scratch = _pool.Rent(upperBound);
        try
        {
            var written = 0;
            foreach (var line in lines)
            {
                written += Encoding.UTF8.GetBytes(line.Code, scratch.AsSpan(written));
                scratch[written++] = (byte)'=';
                written += Encoding.UTF8.GetBytes(line.Value, scratch.AsSpan(written));
                scratch[written++] = (byte)'\n';
            }

            return scratch.AsSpan(0, written).ToArray();
        }
        finally
        {
            _pool.Return(scratch, clearArray: true);
        }
    }

    /// <summary>Decodes the wire format back into manifest lines, the inverse of <see cref="Serialize"/>.</summary>
    public static IReadOnlyList<ManifestLine> Parse(ReadOnlySpan<byte> encoded)
    {
        var lines = new List<ManifestLine>();
        var remaining = encoded;
        while (!remaining.IsEmpty)
        {
            var newlineIndex = remaining.IndexOf((byte)'\n');
            if (newlineIndex < 0)
                break;

            var lineBytes = remaining[..newlineIndex];
            var equalsIndex = lineBytes.IndexOf((byte)'=');
            var code = Encoding.UTF8.GetString(lineBytes[..equalsIndex]);
            var value = Encoding.UTF8.GetString(lineBytes[(equalsIndex + 1)..]);
            lines.Add(new ManifestLine(code, value));

            remaining = remaining[(newlineIndex + 1)..];
        }

        return lines;
    }

    /// <summary>
    /// A deterministic upper bound on the encoded byte length: the worst
    /// case UTF-8 expansion of every code and value, plus one byte each for
    /// the '=' and '\n' separators.
    /// </summary>
    private static int UpperBoundBytes(IReadOnlyList<ManifestLine> lines)
    {
        var total = 0;
        foreach (var line in lines)
        {
            total += Encoding.UTF8.GetMaxByteCount(line.Code.Length);
            total += 1;
            total += Encoding.UTF8.GetMaxByteCount(line.Value.Length);
            total += 1;
        }

        return total;
    }
}
