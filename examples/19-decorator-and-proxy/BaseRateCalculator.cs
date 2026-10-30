namespace RateComposition;

/// <summary>
/// The innermost calculator in the Decorator chain: a flat per-kilogram base
/// rate, with no discount, surcharge, or tax layered on yet.
/// </summary>
public sealed class BaseRateCalculator(decimal ratePerKilogram) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(request.Kilograms * ratePerKilogram);
}
