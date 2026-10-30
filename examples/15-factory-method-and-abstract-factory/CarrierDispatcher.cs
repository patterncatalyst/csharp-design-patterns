namespace CarrierProvisioning;

/// <summary>
/// The Factory Method role: the template steps of dispatching a
/// <see cref="ShipmentRequest"/>, build, send, parse, live here; only which
/// <see cref="ICarrierGateway"/> to send through is left to
/// <see cref="CreateGateway"/>, overridden per carrier. The builder and
/// parser are ordinary constructor-supplied collaborators, not factory
/// methods, because they do not vary independently of the carrier; only the
/// transport does.
/// </summary>
public abstract class CarrierDispatcher
{
    private readonly IShipmentRequestBuilder _builder;
    private readonly IShipmentResponseParser _parser;

    protected CarrierDispatcher(IShipmentRequestBuilder builder, IShipmentResponseParser parser)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(parser);

        _builder = builder;
        _parser = parser;
    }

    /// <summary>
    /// The factory method: each carrier subclass decides which
    /// <see cref="ICarrierGateway"/> <see cref="Dispatch"/> sends through.
    /// </summary>
    protected abstract ICarrierGateway CreateGateway();

    public ShipmentConfirmation Dispatch(ShipmentRequest request)
    {
        var carrierRequest = _builder.Build(request);
        var rawResponse = CreateGateway().Send(carrierRequest);
        return _parser.Parse(rawResponse);
    }
}
