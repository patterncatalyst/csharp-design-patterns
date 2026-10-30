namespace CarrierProvisioning;

/// <summary>
/// The end-to-end entry point: resolves a carrier's whole product family
/// from a <see cref="CarrierRegistry"/> by <typeparamref name="TCarrier"/>
/// and runs build, send, parse through it. The caller never names a
/// concrete gateway, builder, or parser type, only the carrier's reified
/// key type.
/// </summary>
public sealed class ShipmentDispatchService(CarrierRegistry registry)
{
    private readonly CarrierRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public ShipmentConfirmation Dispatch<TCarrier>(ShipmentRequest request)
        where TCarrier : ICarrier
    {
        var factory = _registry.FactoryFor<TCarrier>();
        var carrierRequest = factory.CreateRequestBuilder().Build(request);
        var rawResponse = factory.CreateGateway().Send(carrierRequest);
        return factory.CreateParser().Parse(rawResponse);
    }
}
