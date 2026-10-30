namespace RateComposition;

/// <summary>
/// Decorator that adds a fractional surcharge on top of the wrapped
/// <see cref="IRateCalculator"/>'s quote, for example <c>0.05m</c> for a 5%
/// surcharge. Composes with any other <see cref="IRateCalculator"/>,
/// including another decorator.
/// </summary>
public sealed class SurchargeRateDecorator(IRateCalculator inner, decimal rate) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(inner.Quote(request).Amount * (1m + rate));
}
