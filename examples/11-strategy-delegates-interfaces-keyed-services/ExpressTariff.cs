namespace RateQuoting;

/// <summary>
/// Faster transit at a per-kilogram surcharge over
/// <see cref="StandardTariff.BaseRatePerKilogram"/>.
/// </summary>
public sealed class ExpressTariff : IPricingStrategy
{
    public const decimal SurchargePerKilogram = 2.50m;

    public Money Price(RateRequest request) =>
        Money.Of((StandardTariff.BaseRatePerKilogram + SurchargePerKilogram) * request.WeightKilograms);
}
