using Xunit;

namespace RateComposition.Tests;

/// <summary>
/// Proves the Decorator shape: each layer, discount, surcharge, and tax,
/// implements the same <see cref="IRateCalculator"/> as the
/// <see cref="BaseRateCalculator"/> it wraps, so any combination of layers
/// is itself a drop-in <see cref="IRateCalculator"/>, and removing or
/// reordering a layer changes the quote predictably.
/// </summary>
public class DecoratorChainTests
{
    private static readonly RateRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void The_full_chain_applies_discount_then_surcharge_then_tax_in_order()
    {
        var calculator = new TaxRateDecorator(
            new SurchargeRateDecorator(
                new DiscountRateDecorator(new BaseRateCalculator(5.00m), rate: 0.10m),
                rate: 0.05m),
            rate: 0.08m);

        // Base: 100kg * 5.00 = 500.00
        // Discount 10%:        500.00 * 0.90 = 450.00
        // Surcharge 5%:        450.00 * 1.05 = 472.50
        // Tax 8%:               472.50 * 1.08 = 510.30
        var quote = calculator.Quote(Request);

        Assert.Equal(Money.Of(510.30m), quote);
    }

    [Fact]
    public void Removing_the_surcharge_layer_yields_a_different_predictable_result()
    {
        var calculator = new TaxRateDecorator(
            new DiscountRateDecorator(new BaseRateCalculator(5.00m), rate: 0.10m),
            rate: 0.08m);

        // Base: 500.00, discount 10%: 450.00, tax 8% (no surcharge): 486.00
        var quote = calculator.Quote(Request);

        Assert.Equal(Money.Of(486.00m), quote);
    }

    [Fact]
    public void The_undecorated_base_calculator_differs_from_the_fully_decorated_chain()
    {
        var baseCalculator = new BaseRateCalculator(5.00m);
        var fullChain = new TaxRateDecorator(
            new SurchargeRateDecorator(
                new DiscountRateDecorator(baseCalculator, rate: 0.10m),
                rate: 0.05m),
            rate: 0.08m);

        Assert.NotEqual(baseCalculator.Quote(Request), fullChain.Quote(Request));
    }

    [Fact]
    public void A_single_decorator_layer_is_itself_a_complete_IRateCalculator()
    {
        var discountOnly = new DiscountRateDecorator(new BaseRateCalculator(5.00m), rate: 0.20m);

        var quote = discountOnly.Quote(Request);

        Assert.Equal(Money.Of(400.00m), quote);
    }
}
