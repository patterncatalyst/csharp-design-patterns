namespace CarrierProvisioning;

/// <summary>
/// The Factory Method subclass form for Airco: fixes
/// <see cref="CarrierDispatcher.CreateGateway"/> to <see cref="AircoGateway"/>
/// and wires in Airco's builder and parser.
/// </summary>
public sealed class AircoDispatcher()
    : CarrierDispatcher(new AircoRequestBuilder(), new AircoResponseParser())
{
    protected override ICarrierGateway CreateGateway() => new AircoGateway();
}
