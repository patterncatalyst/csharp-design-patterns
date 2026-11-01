using NSubstitute;
using Xunit;

namespace RateEngineCatalogues.Tests;

/// <summary>
/// The Fowler-stage <see cref="RateEngine"/> under test: a fixed
/// <see cref="FakeClock"/> and either the real <see cref="StandardTariffSource"/>
/// or an NSubstitute substitute stand in for the two collaborators the legacy
/// shape hard-wires, and a <see cref="ServiceSurcharges"/> theory proves the
/// extracted surcharge strategy is selected correctly for every
/// <see cref="ServiceLevel"/>.
/// </summary>
public class FowlerFeathersTests
{
    private static readonly DateTimeOffset OffPeak = new(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Peak = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static RateRequest Request(ServiceLevel service) =>
        new("CN-4301", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, service);

    [Fact]
    public void Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock()
    {
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(OffPeak));

        var quote = engine.Quote(Request(ServiceLevel.Standard));

        Assert.Equal(495.00m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_applies_the_peak_surcharge_during_the_peak_window()
    {
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(Peak));

        var quote = engine.Quote(Request(ServiceLevel.Standard));

        Assert.Equal(544.50m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_applies_the_express_service_surcharge()
    {
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(OffPeak));

        var quote = engine.Quote(Request(ServiceLevel.Express));

        Assert.Equal(618.75m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_applies_the_overnight_service_surcharge()
    {
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(OffPeak));

        var quote = engine.Quote(Request(ServiceLevel.Overnight));

        Assert.Equal(742.50m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_stamps_the_quote_with_the_injected_clock()
    {
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(OffPeak));

        var quote = engine.Quote(Request(ServiceLevel.Standard));

        Assert.Equal(OffPeak, quote.QuotedAt);
    }

    [Fact]
    public void Injected_tariff_source_can_be_substituted()
    {
        var tariffs = Substitute.For<ITariffSource>();
        tariffs.RatePerKilogram("STD").Returns(5.00m);
        var engine = new RateEngine(tariffs, new FakeClock(OffPeak));

        var quote = engine.Quote(Request(ServiceLevel.Standard));

        Assert.Equal(1015.00m, quote.Amount);
        tariffs.Received(1).RatePerKilogram("STD");
    }

    public static TheoryData<ServiceLevel, decimal> ServiceLevelsAndExpectedSubtotals() => new()
    {
        { ServiceLevel.Standard, 100.00m },
        { ServiceLevel.Express, 125.00m },
        { ServiceLevel.Overnight, 150.00m },
    };

    [Theory]
    [MemberData(nameof(ServiceLevelsAndExpectedSubtotals))]
    public void Service_surcharge_is_selected_polymorphically_by_level(ServiceLevel level, decimal expectedSubtotal)
    {
        var surcharge = ServiceSurcharges.For(level);

        var result = surcharge.Apply(100.00m);

        Assert.Equal(expectedSubtotal, result);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(17, true)]
    [InlineData(18, false)]
    [InlineData(23, false)]
    public void Internal_service_rules_are_reachable_through_internals_visible_to(int hour, bool expectedIsPeak)
    {
        var at = new DateTimeOffset(2026, 11, 1, hour, 0, 0, TimeSpan.Zero);

        Assert.Equal(expectedIsPeak, PeakWindow.IsPeak(at));
        Assert.Equal(0.015m, RateRules.DeclaredValueRate);
        Assert.Equal(0.1m, RateRules.PeakMultiplier);
        Assert.Equal("STD", RateRules.StandardLane);
    }

    [Fact]
    public void Standard_tariff_source_returns_the_expected_rate()
    {
        var tariffs = new StandardTariffSource();

        var rate = tariffs.RatePerKilogram("STD");

        Assert.Equal(2.40m, rate);
    }
}
