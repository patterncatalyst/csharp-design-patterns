namespace CarrierProvisioning;

/// <summary>
/// One product of the Abstract Factory family: sends an already-built
/// carrier payload and returns the carrier's raw response. A real
/// implementation would call out over HTTP; these examples compute a
/// deterministic response so the suite needs no network.
/// </summary>
public interface ICarrierGateway
{
    string Send(CarrierRequest request);
}
