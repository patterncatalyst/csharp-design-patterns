using NSubstitute;
using Xunit;

namespace RateEngineReplacement.Tests;

/// <summary>
/// Both replacements for the legacy engine, <see cref="RefactoredRateEngine"/>
/// and <see cref="RewrittenRateEngine"/>, under test with a fixed
/// <see cref="FakeClock"/> and either the real <see cref="StandardTariffSource"/>
/// or an NSubstitute substitute standing in for the collaborators the legacy
/// shape hard-wires. <see cref="QuoteService"/> is exercised against both
/// engines through the same caller code to show the <see cref="IRateEngine"/>
/// seam making the choice between them a one-line swap.
/// </summary>
public class RefactorOrRewriteTests
{
    private static readonly DateTimeOffset OffPeak = new(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Peak = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly RateRequest Request = new("CN-4101", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, LaneCode: "STD");

    public static TheoryData<Func<ITariffSource, IClock, IRateEngine>> EngineFactories => new()
    {
        (tariffs, clock) => new RefactoredRateEngine(tariffs, clock),
        (tariffs, clock) => new RewrittenRateEngine(tariffs, clock),
    };

    [Fact]
    public void Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock()
    {
        var engine = new RefactoredRateEngine(new StandardTariffSource(), new FakeClock(OffPeak));

        var quote = engine.Quote(Request);

        Assert.Equal(495.00m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_applies_the_peak_surcharge_during_the_peak_window()
    {
        var engine = new RefactoredRateEngine(new StandardTariffSource(), new FakeClock(Peak));

        var quote = engine.Quote(Request);

        Assert.Equal(544.50m, quote.Amount);
    }

    [Fact]
    public void Rewritten_engine_quotes_a_deterministic_amount_with_a_fixed_clock()
    {
        var engine = new RewrittenRateEngine(new StandardTariffSource(), new FakeClock(OffPeak));

        var quote = engine.Quote(Request);

        Assert.Equal(495.00m, quote.Amount);
    }

    [Fact]
    public void Rewritten_engine_applies_the_peak_surcharge_during_the_peak_window()
    {
        var engine = new RewrittenRateEngine(new StandardTariffSource(), new FakeClock(Peak));

        var quote = engine.Quote(Request);

        Assert.Equal(544.50m, quote.Amount);
    }

    [Fact]
    public void Quote_service_works_with_the_refactored_engine_behind_the_seam()
    {
        var service = new QuoteService(new RefactoredRateEngine(new StandardTariffSource(), new FakeClock(OffPeak)));

        var quote = service.QuoteFor(Request);

        Assert.Equal(495.00m, quote.Amount);
    }

    [Fact]
    public void Quote_service_works_with_the_rewritten_engine_behind_the_seam()
    {
        // Same caller code as the refactored case above: only the engine
        // passed to QuoteService's constructor changes.
        var service = new QuoteService(new RewrittenRateEngine(new StandardTariffSource(), new FakeClock(OffPeak)));

        var quote = service.QuoteFor(Request);

        Assert.Equal(495.00m, quote.Amount);
    }

    [Theory]
    [MemberData(nameof(EngineFactories))]
    public void Injected_tariff_source_can_be_substituted_in_both_engines(
        Func<ITariffSource, IClock, IRateEngine> createEngine)
    {
        var tariffs = Substitute.For<ITariffSource>();
        tariffs.RatePerKilogram("STD").Returns(5.00m);
        var engine = createEngine(tariffs, new FakeClock(OffPeak));

        var quote = engine.Quote(Request);

        Assert.Equal(1015.00m, quote.Amount);
        tariffs.Received(1).RatePerKilogram("STD");
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(17, true)]
    [InlineData(18, false)]
    [InlineData(23, false)]
    public void Internal_peak_window_is_reachable_through_internals_visible_to(int hour, bool expectedIsPeak)
    {
        var at = new DateTimeOffset(2026, 11, 1, hour, 0, 0, TimeSpan.Zero);

        var isPeak = PeakWindow.IsPeak(at);

        Assert.Equal(expectedIsPeak, isPeak);
    }

    [Fact]
    public void Standard_tariff_source_returns_the_expected_rate()
    {
        var tariffs = new StandardTariffSource();

        var rate = tariffs.RatePerKilogram("STD");

        Assert.Equal(2.40m, rate);
    }
}
