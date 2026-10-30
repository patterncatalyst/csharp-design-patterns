namespace RateQuoting;

/// <summary>
/// Tier 2: Strategy as the textbook GoF shape, a constructor-injected
/// <see cref="IPricingStrategy"/>. Swapping the algorithm means constructing
/// this class with a different implementation; the class itself never
/// changes.
/// </summary>
public sealed class StrategyRateQuoter(IPricingStrategy strategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = strategy.Price(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}
