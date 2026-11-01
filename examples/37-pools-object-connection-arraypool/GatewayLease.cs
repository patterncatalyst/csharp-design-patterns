namespace GatewayPooling;

/// <summary>
/// An RAII-style handle on a borrowed connection: disposing the lease
/// returns the connection to the pool it came from, so a <c>using</c> block
/// is enough to guarantee the connection is never leaked even if the caller
/// throws. A record struct gets structural equality for free, which matters
/// for a value that is naturally compared by its pool and connection rather
/// than by reference identity.
/// </summary>
public readonly record struct GatewayLease(GatewayConnectionPool Pool, IGatewayConnection Connection)
    : IDisposable
{
    public void Dispose() => Pool.Return(Connection);
}
