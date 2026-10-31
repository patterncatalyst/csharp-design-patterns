namespace CarrierManifest;

/// <summary>
/// Hand-written consumer of the generator's output. Nothing in this class
/// references a specific generated gateway type by name; it only goes through
/// <see cref="GeneratedCarrierRegistry"/>, which proves the generated code
/// compiles and is callable from ordinary application code. Each dispatch is
/// recorded on the instance so a caller can audit what was routed where.
/// </summary>
public sealed class CarrierDispatch
{
    private readonly List<string> _dispatchLog = [];

    /// <summary>
    /// Resolves the routing endpoint for a carrier code, records the
    /// dispatch, and returns the endpoint. Throws if the manifest has no
    /// entry for <paramref name="code"/>.
    /// </summary>
    public string ResolveEndpoint(string code)
    {
        var gateway = GeneratedCarrierRegistry.Find(code);
        if (gateway is null)
        {
            throw new InvalidOperationException($"No carrier gateway registered for code '{code}'.");
        }

        _dispatchLog.Add($"{gateway.Code} -> {gateway.Endpoint}");
        return gateway.Endpoint;
    }

    /// <summary>
    /// Every carrier code the manifest produced a gateway for, in manifest
    /// order. Static: this reads only the generated registry, never this
    /// instance's dispatch log.
    /// </summary>
    public static IReadOnlyList<string> KnownCarrierCodes() =>
        GeneratedCarrierRegistry.All.Select(gateway => gateway.Code).ToList();

    /// <summary>
    /// The "carrier code -> endpoint" trail recorded by every successful
    /// <see cref="ResolveEndpoint"/> call on this instance, oldest first.
    /// </summary>
    public IReadOnlyList<string> DispatchLog => _dispatchLog;
}
