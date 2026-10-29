using NSubstitute;
using Xunit;

namespace RatingEngine.Tests;

/// <summary>
/// Parametrized tests: <c>[InlineData]</c> for a handful of literal cases, and
/// a <see cref="TheoryData{T1, T2}"/>-typed <c>[MemberData]</c> source for
/// cases worth naming rather than indexing into positional arguments.
/// </summary>
public class TheoryTests
{
    [Theory]
    [InlineData("Rotterdam", 100.0)]
    [InlineData("Santos", 250.0)]
    [InlineData("Busan", 900.0)]
    public async Task Passes_the_destination_and_weight_through_to_the_carrier_rate_lookup(
        string destination, decimal kilograms)
    {
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync(destination, kilograms, Arg.Any<CancellationToken>())
            .Returns(new Money(50.00m));
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(50.00m)).Returns(new Money(50.00m));
        var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
        var consignment = new Consignment("C-0002", destination, kilograms);

        await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        await carrierRates.Received(1).GetBaseRateAsync(destination, kilograms, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A weight tier and the carrier's quoted base rate for it. A
    /// <see cref="TheoryData{T1, T2}"/> source (rather than a bare
    /// <c>IEnumerable&lt;object[]&gt;</c>) keeps each case strongly typed and
    /// satisfies the analyzer rules that flag untyped member data.
    /// </summary>
    public static TheoryData<decimal, decimal> WeightTierBaseRates() => new()
    {
        { 10m, 25.00m },
        { 500m, 625.00m },
        { 2000m, 2400.00m },
    };

    [Theory]
    [MemberData(nameof(WeightTierBaseRates))]
    public async Task Returns_the_carriers_quoted_base_rate_for_each_weight_tier(
        decimal kilograms, decimal baseRateAmount)
    {
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync("Rotterdam", kilograms, Arg.Any<CancellationToken>())
            .Returns(new Money(baseRateAmount));
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(baseRateAmount)).Returns(new Money(baseRateAmount));
        var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
        var consignment = new Consignment("C-0003", "Rotterdam", kilograms);

        var quote = await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        Assert.Equal(new Money(baseRateAmount), quote);
    }
}
