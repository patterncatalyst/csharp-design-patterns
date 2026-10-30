namespace CarrierProvisioning;

/// <summary>
/// The Factory Method subclass form for Seabright: fixes
/// <see cref="CarrierDispatcher.CreateGateway"/> to <see cref="SeabrightGateway"/>
/// and wires in Seabright's builder and parser.
/// </summary>
public sealed class SeabrightDispatcher()
    : CarrierDispatcher(new SeabrightRequestBuilder(), new SeabrightResponseParser())
{
    protected override ICarrierGateway CreateGateway() => new SeabrightGateway();
}
