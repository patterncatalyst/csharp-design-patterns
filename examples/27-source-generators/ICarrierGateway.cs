namespace CarrierManifest;

/// <summary>
/// Contract every carrier gateway implements, hand-written and real at
/// compile time before the generator ever runs. The generator targets this
/// interface by name for each manifest row; it never defines or changes the
/// contract itself.
/// </summary>
public interface ICarrierGateway
{
    string Code { get; }

    string DisplayName { get; }

    string Endpoint { get; }
}
