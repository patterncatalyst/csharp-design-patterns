namespace CarrierProvisioning;

/// <summary>
/// The Factory Method subclass form for Fastline: fixes
/// <see cref="CarrierDispatcher.CreateGateway"/> to <see cref="FastlineGateway"/>
/// and wires in Fastline's builder and parser.
/// </summary>
public sealed class FastlineDispatcher()
    : CarrierDispatcher(new FastlineRequestBuilder(), new FastlineResponseParser())
{
    protected override ICarrierGateway CreateGateway() => new FastlineGateway();
}
