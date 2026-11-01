using System.Buffers;

namespace GatewayPooling.Tests;

/// <summary>
/// An <see cref="ArrayPool{T}"/> test double that wraps a dedicated
/// <see cref="ArrayPool{T}.Create()"/> instance (never
/// <see cref="ArrayPool{T}.Shared"/>, whose bucket contents are shared
/// process-wide and not deterministic across test runs) and records every
/// rent and return so a test can assert reuse and clearing behavior.
/// </summary>
public sealed class RecordingArrayPool : ArrayPool<byte>
{
    private readonly ArrayPool<byte> _inner = Create();

    public int RentCount { get; private set; }

    public int ReturnCount { get; private set; }

    public bool LastReturnedCleared { get; private set; }

    public byte[]? LastRented { get; private set; }

    public byte[]? LastReturned { get; private set; }

    public override byte[] Rent(int minimumLength)
    {
        var array = _inner.Rent(minimumLength);
        RentCount++;
        LastRented = array;
        return array;
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ReturnCount++;
        LastReturned = array;
        LastReturnedCleared = clearArray;
        _inner.Return(array, clearArray);
    }
}
