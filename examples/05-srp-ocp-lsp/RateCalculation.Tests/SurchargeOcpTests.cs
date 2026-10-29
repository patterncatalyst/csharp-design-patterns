using Xunit;

namespace RateCalculation.Tests;

/// <summary>
/// OCP: a brand-new surcharge type composes into the pricing pipeline without
/// a single edit to any existing pricing or surcharge type.
/// </summary>
public class SurchargeOcpTests
{
    private static Consignment SampleConsignment() => new(new Weight(10m), new Money(1000m, "USD"));

    [Fact]
    public void A_single_surcharge_adds_on_top_of_the_base_price()
    {
        var quote = new SurchargedRateQuote(
            new WeightBasedRateCalculator(ratePerKilogram: 2.50m),
            [new FuelSurcharge(rateOfSubtotal: 0.10m)]);

        var total = quote.Quote(SampleConsignment());

        // 25.00 base + 10% fuel surcharge
        Assert.Equal(new Money(27.50m, "USD"), total);
    }

    /// <summary>
    /// A brand-new surcharge type, introduced only in this test file. It
    /// implements <see cref="ISurcharge"/> and composes alongside
    /// <see cref="FuelSurcharge"/> without a single line changing in
    /// <see cref="WeightBasedRateCalculator"/>, <see cref="TieredRateCalculator"/>,
    /// <see cref="SurchargedRateQuote"/>, or <see cref="FuelSurcharge"/> itself:
    /// the pricing pipeline is open for this extension, closed for modification.
    /// </summary>
    private sealed class HazmatHandlingSurcharge(Money flatFee) : ISurcharge
    {
        public Money Apply(Money subtotal, Consignment consignment) => flatFee;
    }

    [Fact]
    public void A_newly_added_surcharge_type_composes_without_editing_any_pricing_type()
    {
        var quote = new SurchargedRateQuote(
            new WeightBasedRateCalculator(ratePerKilogram: 2.50m),
            [new FuelSurcharge(rateOfSubtotal: 0.10m), new HazmatHandlingSurcharge(new Money(15.00m, "USD"))]);

        var total = quote.Quote(SampleConsignment());

        // 25.00 base + 2.50 fuel + 15.00 hazmat handling
        Assert.Equal(new Money(42.50m, "USD"), total);
    }
}
