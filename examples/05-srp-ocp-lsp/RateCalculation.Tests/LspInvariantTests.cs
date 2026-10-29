using Xunit;

namespace RateCalculation.Tests;

/// <summary>
/// A meaningful Liskov substitution test: every <see cref="IRateQuote"/> and
/// <see cref="ISurcharge"/> implementation must honour the same behavioural
/// contract regardless of its own pricing formula. This is not a
/// type-compatibility check; every case calls the implementation and asserts
/// on its actual results.
/// </summary>
public class LspInvariantTests
{
    private static Consignment LightConsignment() => new(new Weight(10m), new Money(1000m, "USD"));

    private static Consignment HeavyConsignment() => new(new Weight(50m), new Money(5000m, "USD"));

    public static TheoryData<string, Func<IRateQuote>> RateQuoteImplementations() => new()
    {
        { nameof(WeightBasedRateCalculator), () => new WeightBasedRateCalculator(ratePerKilogram: 2.50m) },
        {
            nameof(TieredRateCalculator), () => new TieredRateCalculator(
                firstTierKilograms: 20m, firstTierRatePerKilogram: 3.00m, additionalRatePerKilogram: 2.00m)
        },
    };

    public static TheoryData<string, Func<ISurcharge>> SurchargeImplementations() => new()
    {
        { nameof(FuelSurcharge), () => new FuelSurcharge(rateOfSubtotal: 0.10m) },
        { nameof(InsuranceSurcharge), () => new InsuranceSurcharge(rateOfDeclaredValue: 0.01m) },
    };

    [Theory]
    [MemberData(nameof(RateQuoteImplementations))]
    public void Every_rate_quote_returns_a_non_negative_amount(string _, Func<IRateQuote> makeQuote)
    {
        var quote = makeQuote().Quote(LightConsignment());

        Assert.True(quote.Amount >= 0m);
    }

    [Theory]
    [MemberData(nameof(RateQuoteImplementations))]
    public void Every_rate_quote_preserves_the_consignment_currency(string _, Func<IRateQuote> makeQuote)
    {
        var consignment = LightConsignment();

        var quote = makeQuote().Quote(consignment);

        Assert.Equal(consignment.DeclaredValue.Currency, quote.Currency);
    }

    [Theory]
    [MemberData(nameof(RateQuoteImplementations))]
    public void Every_rate_quote_leaves_the_consignment_untouched(string _, Func<IRateQuote> makeQuote)
    {
        var consignment = LightConsignment();
        var consignmentBeforeQuoting = consignment with { };

        makeQuote().Quote(consignment);

        Assert.Equal(consignmentBeforeQuoting, consignment);
    }

    [Theory]
    [MemberData(nameof(RateQuoteImplementations))]
    public void Every_rate_quote_charges_at_least_as_much_for_a_heavier_consignment(
        string _, Func<IRateQuote> makeQuote)
    {
        var calculator = makeQuote();

        var lightQuote = calculator.Quote(LightConsignment());
        var heavyQuote = calculator.Quote(HeavyConsignment());

        Assert.True(heavyQuote.Amount >= lightQuote.Amount);
    }

    [Theory]
    [MemberData(nameof(SurchargeImplementations))]
    public void Every_surcharge_returns_a_non_negative_amount(string _, Func<ISurcharge> makeSurcharge)
    {
        var baseQuote = new WeightBasedRateCalculator(ratePerKilogram: 2.50m).Quote(LightConsignment());

        var charge = makeSurcharge().Apply(baseQuote, LightConsignment());

        Assert.True(charge.Amount >= 0m);
    }

    [Theory]
    [MemberData(nameof(SurchargeImplementations))]
    public void Every_surcharge_preserves_the_subtotal_currency(string _, Func<ISurcharge> makeSurcharge)
    {
        var consignment = LightConsignment();
        var baseQuote = new WeightBasedRateCalculator(ratePerKilogram: 2.50m).Quote(consignment);

        var charge = makeSurcharge().Apply(baseQuote, consignment);

        Assert.Equal(baseQuote.Currency, charge.Currency);
    }

    [Theory]
    [MemberData(nameof(SurchargeImplementations))]
    public void Every_surcharge_leaves_the_consignment_untouched(string _, Func<ISurcharge> makeSurcharge)
    {
        var consignment = LightConsignment();
        var consignmentBeforeCharging = consignment with { };
        var baseQuote = new WeightBasedRateCalculator(ratePerKilogram: 2.50m).Quote(consignment);

        makeSurcharge().Apply(baseQuote, consignment);

        Assert.Equal(consignmentBeforeCharging, consignment);
    }

    [Theory]
    [MemberData(nameof(SurchargeImplementations))]
    public void Every_surcharge_charges_at_least_as_much_for_a_heavier_consignment(
        string _, Func<ISurcharge> makeSurcharge)
    {
        var pricing = new WeightBasedRateCalculator(ratePerKilogram: 2.50m);
        var light = LightConsignment();
        var heavy = HeavyConsignment();

        var lightCharge = makeSurcharge().Apply(pricing.Quote(light), light);
        var heavyCharge = makeSurcharge().Apply(pricing.Quote(heavy), heavy);

        Assert.True(heavyCharge.Amount >= lightCharge.Amount);
    }
}
