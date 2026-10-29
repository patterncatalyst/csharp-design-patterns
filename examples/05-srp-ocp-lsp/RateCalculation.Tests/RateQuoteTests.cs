using Xunit;

namespace RateCalculation.Tests;

/// <summary>
/// SRP: each pricing calculator is constructed and proven correct entirely on
/// its own, with no tax policy and no audit sink in the picture.
/// </summary>
public class RateQuoteTests
{
    private static Consignment LightConsignment() => new(new Weight(10m), new Money(1000m, "USD"));

    private static Consignment HeavyConsignment() => new(new Weight(50m), new Money(5000m, "USD"));

    [Fact]
    public void WeightBasedRateCalculator_prices_by_rate_times_weight()
    {
        var calculator = new WeightBasedRateCalculator(ratePerKilogram: 2.50m);

        var quote = calculator.Quote(LightConsignment());

        Assert.Equal(new Money(25.00m, "USD"), quote);
    }

    [Fact]
    public void WeightBasedRateCalculator_needs_no_tax_or_audit_collaborator()
    {
        // SRP: constructed with only a rate, no ITaxPolicy and no IAuditSink,
        // and it still produces a correct quote on its own.
        var pricingOnly = new WeightBasedRateCalculator(ratePerKilogram: 2.50m);

        var quote = pricingOnly.Quote(HeavyConsignment());

        Assert.Equal(new Money(125.00m, "USD"), quote);
    }

    [Fact]
    public void TieredRateCalculator_bills_the_first_tier_then_the_additional_rate()
    {
        var calculator = new TieredRateCalculator(
            firstTierKilograms: 20m, firstTierRatePerKilogram: 3.00m, additionalRatePerKilogram: 2.00m);

        var quote = calculator.Quote(HeavyConsignment());

        // 20 kg at 3.00 + 30 kg at 2.00
        Assert.Equal(new Money(120.00m, "USD"), quote);
    }

    [Fact]
    public void TieredRateCalculator_bills_entirely_at_the_first_tier_rate_under_the_threshold()
    {
        var calculator = new TieredRateCalculator(
            firstTierKilograms: 20m, firstTierRatePerKilogram: 3.00m, additionalRatePerKilogram: 2.00m);

        var quote = calculator.Quote(LightConsignment());

        Assert.Equal(new Money(30.00m, "USD"), quote);
    }
}
