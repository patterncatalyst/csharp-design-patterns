namespace RateComposition;

/// <summary>
/// Decorator that adds tax on top of the wrapped <see cref="IRateCalculator"/>'s
/// quote, for example <c>0.08m</c> for an 8% tax. Placed outermost in the
/// canonical chain so tax is computed on the discounted, surcharged amount,
/// not the bare base rate.
/// </summary>
public sealed class TaxRateDecorator(IRateCalculator inner, decimal rate) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(inner.Quote(request).Amount * (1m + rate));
}
