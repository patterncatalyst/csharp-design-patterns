namespace CarrierProvisioning;

/// <summary>
/// Resolves a carrier's <see cref="ICarrierGatewayFactory{TCarrier}"/> by
/// <c>typeof(TCarrier)</c> rather than by a string key or a switch over a
/// carrier code. Reified generics give <see cref="Register{TCarrier}"/> and
/// <see cref="FactoryFor{TCarrier}"/> a type parameter the runtime actually
/// carries: the stored factory comes back out already cast to
/// <see cref="ICarrierGatewayFactory{TCarrier}"/>, not <c>object</c>, so a
/// caller never casts by hand. Adding a new carrier is one
/// <see cref="Register{TCarrier}"/> call; this class never changes.
/// </summary>
public sealed class CarrierRegistry
{
    private readonly Dictionary<Type, object> _factories = new();

    public void Register<TCarrier>(ICarrierGatewayFactory<TCarrier> factory)
        where TCarrier : ICarrier
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factories[typeof(TCarrier)] = factory;
    }

    public ICarrierGatewayFactory<TCarrier> FactoryFor<TCarrier>()
        where TCarrier : ICarrier
    {
        if (_factories.TryGetValue(typeof(TCarrier), out var factory))
        {
            return (ICarrierGatewayFactory<TCarrier>)factory;
        }

        throw new UnknownCarrierException(
            $"No gateway factory is registered for carrier type '{typeof(TCarrier).Name}'.");
    }
}
