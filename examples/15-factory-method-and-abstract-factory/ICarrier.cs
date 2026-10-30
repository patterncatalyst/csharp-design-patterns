namespace CarrierProvisioning;

/// <summary>
/// A carrier identity, reified as a type rather than a string constant. The
/// <c>static abstract</c> member gives every implementer real content (its
/// wire code), so the marker interface itself carries meaning instead of
/// standing in as an empty tag. <see cref="CarrierRegistry"/> and
/// <see cref="ICarrierGatewayFactory{TCarrier}"/> are both generic over
/// <typeparamref name="ICarrier"/>-constrained type parameters, so the
/// compiler, not a string key, ties a carrier's gateway, builder, and parser
/// together.
/// </summary>
public interface ICarrier
{
    static abstract string Code { get; }
}
