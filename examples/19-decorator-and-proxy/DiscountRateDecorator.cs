namespace RateComposition;

/// <summary>
/// Decorator that reduces the wrapped <see cref="IRateCalculator"/>'s quote
/// by a fractional discount rate, for example <c>0.10m</c> for a 10%
/// discount. Composes with any other <see cref="IRateCalculator"/>,
/// including another decorator.
/// </summary>
public sealed class DiscountRateDecorator(IRateCalculator inner, decimal rate) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(inner.Quote(request).Amount * (1m - rate));
}
