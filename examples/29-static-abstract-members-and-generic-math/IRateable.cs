namespace RateMath;

/// <summary>
/// A shipment type that knows, as a static abstract contract, both its own
/// base rate and how to compute a quote for a concrete instance of itself.
/// The self-constraint (<c>where TSelf : IRateable&lt;TSelf&gt;</c>) is what
/// lets <see cref="RateBook"/> call <c>T.BaseRate</c> and <c>T.RateOf</c>
/// through the type parameter alone, with no instance to dispatch on.
/// </summary>
/// <typeparam name="TSelf">The concrete shipment type implementing the contract.</typeparam>
public interface IRateable<TSelf> where TSelf : IRateable<TSelf>
{
    /// <summary>The flat base rate charged before any shipment-specific surcharge.</summary>
    static abstract Money BaseRate { get; }

    /// <summary>Computes the full quote for one shipment of this type.</summary>
    static abstract Money RateOf(TSelf shipment);
}
