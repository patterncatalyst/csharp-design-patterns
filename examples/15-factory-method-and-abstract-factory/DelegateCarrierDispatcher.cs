namespace CarrierProvisioning;

/// <summary>
/// The delegate alternative to <see cref="CarrierDispatcher"/>: the same
/// build-send-parse template, but the factory method is a plain
/// <see cref="Func{TResult}"/> constructor parameter instead of a subclass
/// override. Reaching parity with a <see cref="CarrierDispatcher"/>
/// subclass for the same carrier is passing that carrier's own gateway,
/// builder, and parser.
/// </summary>
public sealed class DelegateCarrierDispatcher(
    Func<ICarrierGateway> gatewayFactory,
    IShipmentRequestBuilder builder,
    IShipmentResponseParser parser)
{
    private readonly Func<ICarrierGateway> _gatewayFactory = gatewayFactory
        ?? throw new ArgumentNullException(nameof(gatewayFactory));
    private readonly IShipmentRequestBuilder _builder = builder
        ?? throw new ArgumentNullException(nameof(builder));
    private readonly IShipmentResponseParser _parser = parser
        ?? throw new ArgumentNullException(nameof(parser));

    public ShipmentConfirmation Dispatch(ShipmentRequest request)
    {
        var carrierRequest = _builder.Build(request);
        var rawResponse = _gatewayFactory().Send(carrierRequest);
        return _parser.Parse(rawResponse);
    }
}
