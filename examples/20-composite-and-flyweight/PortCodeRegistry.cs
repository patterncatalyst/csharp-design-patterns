using System.Collections.Frozen;

namespace ConsignmentRouting;

/// <summary>
/// Interns one <see cref="PortCode"/> instance per UN/LOCODE and hands the
/// same instance back on every lookup. The seed codes are the small, fixed
/// set a bulk carrier actually calls at; a new call isn't a reason to edit
/// every parcel that already names a port, only to add a row here.
/// </summary>
public sealed class PortCodeRegistry
{
    private static readonly PortCode[] SeedPorts =
    [
        new PortCode("NLRTM", "Rotterdam"),
        new PortCode("USLAX", "Los Angeles"),
        new PortCode("SGSIN", "Singapore"),
        new PortCode("DEHAM", "Hamburg"),
        new PortCode("CNSHA", "Shanghai"),
    ];

    private readonly FrozenDictionary<string, PortCode> _byCode =
        SeedPorts.ToFrozenDictionary(port => port.Code);

    /// <summary>
    /// Returns the shared <see cref="PortCode"/> instance for
    /// <paramref name="code"/>, or throws <see cref="UnknownPortCodeException"/>
    /// if the registry never interned it.
    /// </summary>
    public PortCode Get(string code) => _byCode.TryGetValue(code, out var port)
        ? port
        : throw new UnknownPortCodeException($"No port code is registered for '{code}'.");

    /// <summary>
    /// The non-throwing counterpart to <see cref="Get"/>, for a caller that
    /// expects an unknown code to be a routine case rather than a fault.
    /// </summary>
    public bool TryGet(string code, out PortCode? port) => _byCode.TryGetValue(code, out port);
}
