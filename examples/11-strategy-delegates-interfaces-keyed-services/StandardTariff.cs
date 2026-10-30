namespace RateQuoting;

/// <summary>
/// The baseline tariff: a flat per-kilogram rate with no surcharge or
/// discount. <see cref="ExpressTariff"/> and <see cref="EconomyTariff"/> both
/// price relative to <see cref="BaseRatePerKilogram"/>.
/// </summary>
public sealed class StandardTariff : IPricingStrategy
{
    public const decimal BaseRatePerKilogram = 4.00m;

    public Money Price(RateRequest request) => Money.Of(BaseRatePerKilogram * request.WeightKilograms);
}
