namespace RateQuoting;

/// <summary>
/// Slower transit at a per-kilogram discount under
/// <see cref="StandardTariff.BaseRatePerKilogram"/>.
/// </summary>
public sealed class EconomyTariff : IPricingStrategy
{
    public const decimal DiscountPerKilogram = 1.00m;

    public Money Price(RateRequest request) =>
        Money.Of((StandardTariff.BaseRatePerKilogram - DiscountPerKilogram) * request.WeightKilograms);
}
