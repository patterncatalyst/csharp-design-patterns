namespace RateMath;

/// <summary>
/// A generic rate-quoting algorithm written once against the
/// <see cref="IRateable{TSelf}"/> contract. Neither method has an instance of
/// <typeparamref name="T"/> handy for <c>BaseRateOf</c>, and <c>Quote</c> never
/// branches on the concrete type: both static members are looked up through
/// the type parameter at the call site, and the JIT specializes the call per
/// value-type <c>T</c>.
/// </summary>
public static class RateBook
{
    /// <summary>Quotes a single shipment by dispatching to its own static <c>RateOf</c>.</summary>
    public static Money Quote<T>(T shipment) where T : IRateable<T>
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return T.RateOf(shipment);
    }

    /// <summary>Reads a shipment type's base rate without constructing an instance.</summary>
    public static Money BaseRateOf<T>() where T : IRateable<T> => T.BaseRate;
}
