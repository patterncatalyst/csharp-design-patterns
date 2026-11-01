namespace GatewayPooling;

/// <summary>
/// A single connection to a carrier gateway. Establishing one is assumed to
/// be expensive enough (handshake, authentication) that a caller borrows an
/// existing connection from a <see cref="GatewayConnectionPool"/> instead of
/// opening a fresh one per manifest exchange.
/// </summary>
public interface IGatewayConnection
{
    /// <summary>Stable identity, distinct for every connection a pool creates.</summary>
    int Id { get; }

    /// <summary>Exchanges a manifest payload with the carrier gateway over this connection.</summary>
    Task<string> ExchangeAsync(string manifest, CancellationToken ct);
}
