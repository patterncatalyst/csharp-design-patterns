namespace GatewayPooling;

/// <summary>
/// A real (in this example, simulated) carrier gateway connection. The
/// handshake cost that makes pooling worthwhile happens once, in whatever
/// constructs this instance; <see cref="ExchangeAsync"/> itself is just the
/// per-manifest request/response.
/// </summary>
public sealed class GatewayConnection : IGatewayConnection
{
    public GatewayConnection(int id)
    {
        Id = id;
    }

    public int Id { get; }

    /// <summary>
    /// Sends a manifest over the wire and returns the gateway's
    /// acknowledgement. <see cref="Task.Yield"/> stands in for the network
    /// round trip so the method is a genuine asynchronous continuation
    /// without a wall-clock wait.
    /// </summary>
    public async Task<string> ExchangeAsync(string manifest, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        return $"ACK:{manifest.Length}";
    }
}
