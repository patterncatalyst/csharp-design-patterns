namespace RateComposition;

/// <summary>
/// Prices a <see cref="RateRequest"/>. <see cref="BaseRateCalculator"/> is
/// the innermost implementation; <see cref="DiscountRateDecorator"/>,
/// <see cref="SurchargeRateDecorator"/>, and <see cref="TaxRateDecorator"/>
/// each wrap an <see cref="IRateCalculator"/> and implement the same
/// interface, so any combination of layers is itself an
/// <see cref="IRateCalculator"/> a caller can quote without knowing how many
/// layers it is built from.
/// </summary>
public interface IRateCalculator
{
    Money Quote(RateRequest request);
}
