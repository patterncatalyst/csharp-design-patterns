using System.Threading.Channels;

namespace GatewayPooling;

/// <summary>
/// A fixed-size connection pool over <see cref="IGatewayConnection"/>. The
/// available set lives in a <see cref="Channel{T}"/>, which does double duty
/// as both the storage for idle connections and the async-aware wait queue
/// for a renter that arrives when the pool is exhausted: reading from an
/// empty channel suspends the caller until <see cref="Return"/> writes a
/// connection back, with no polling and no wall-clock timeout.
/// </summary>
public sealed class GatewayConnectionPool : IAsyncDisposable
{
    private readonly Channel<IGatewayConnection> _available = Channel.CreateUnbounded<IGatewayConnection>();

    public GatewayConnectionPool(int size, Func<int, IGatewayConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        Size = size;
        for (var i = 0; i < size; i++)
            _available.Writer.TryWrite(connectionFactory(i));
    }

    /// <summary>The fixed number of connections this pool was created with.</summary>
    public int Size { get; }

    /// <summary>The number of connections currently idle in the pool.</summary>
    public int Available => _available.Reader.Count;

    /// <summary>
    /// Borrows a connection, suspending the caller if none is idle until one
    /// is returned. Callers that want automatic return should prefer
    /// <see cref="RentLeaseAsync"/>.
    /// </summary>
    public ValueTask<IGatewayConnection> RentAsync(CancellationToken ct = default) =>
        _available.Reader.ReadAsync(ct);

    /// <summary>Returns a connection to the pool, unblocking the oldest waiting renter, if any.</summary>
    public void Return(IGatewayConnection connection) => _available.Writer.TryWrite(connection);

    /// <summary>Borrows a connection wrapped in a disposable lease that returns it automatically.</summary>
    public async ValueTask<GatewayLease> RentLeaseAsync(CancellationToken ct = default) =>
        new(this, await RentAsync(ct));

    /// <summary>
    /// Closes the pool to further use and disposes every connection
    /// currently idle in it. Sealed with no finalizer, so there is nothing
    /// for <see cref="GC.SuppressFinalize"/> to suppress.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _available.Writer.Complete();
        await foreach (var connection in _available.Reader.ReadAllAsync())
        {
            if (connection is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
