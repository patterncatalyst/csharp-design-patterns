namespace RateQuoting;

/// <summary>
/// Tier 1: Strategy dissolved to a plain delegate. There is no
/// <see cref="IPricingStrategy"/> here at all, just a
/// <see cref="Func{RateRequest, Money}"/> the caller supplies; swapping the
/// pricing algorithm is passing a different lambda to the constructor, not
/// implementing a new type.
/// </summary>
public sealed class DelegateRateQuoter(Func<RateRequest, Money> priceStrategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = priceStrategy(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}
