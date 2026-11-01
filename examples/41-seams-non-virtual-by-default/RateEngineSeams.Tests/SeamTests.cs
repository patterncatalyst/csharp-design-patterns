using NSubstitute;
using Xunit;

namespace RateEngineSeams.Tests;

/// <summary>
/// The refactored, seam-enabled <see cref="RateEngine"/> under test: a fixed
/// <see cref="FakeClock"/> and either the real <see cref="StandardTariffSource"/>
/// or an NSubstitute substitute stand in for the two collaborators the legacy
/// shape hard-wires, and <see cref="TestableRateEngine"/> shows the
/// extract-and-override seam over the surcharge rule itself.
/// </summary>
public class SeamTests
{
    private static readonly RateRequest Request = new("CN-4101", DeclaredValue: 1000.00m, WeightKilograms: 200.00m);

    [Fact]
    public void Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock()
    {
        var offPeak = new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(offPeak));

        var quote = engine.Quote(Request);

        Assert.Equal(495.00m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_applies_the_peak_surcharge_during_the_peak_window()
    {
        var peak = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(peak));

        var quote = engine.Quote(Request);

        Assert.Equal(544.50m, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_omits_the_peak_surcharge_outside_the_peak_window()
    {
        var offPeak = new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(offPeak));

        var quote = engine.Quote(Request);

        const decimal SubtotalWithNoSurcharge = 495.00m;
        Assert.Equal(SubtotalWithNoSurcharge, quote.Amount);
    }

    [Fact]
    public void Refactored_engine_stamps_the_quote_with_the_injected_clock()
    {
        var fixedInstant = new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
        var engine = new RateEngine(new StandardTariffSource(), new FakeClock(fixedInstant));

        var quote = engine.Quote(Request);

        Assert.Equal(fixedInstant, quote.QuotedAt);
    }

    [Fact]
    public void Injected_tariff_source_can_be_substituted()
    {
        var tariffs = Substitute.For<ITariffSource>();
        tariffs.RatePerKilogram("STD").Returns(5.00m);
        var offPeak = new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
        var engine = new RateEngine(tariffs, new FakeClock(offPeak));

        var quote = engine.Quote(Request);

        Assert.Equal(1015.00m, quote.Amount);
        tariffs.Received(1).RatePerKilogram("STD");
    }

    [Fact]
    public void Extract_and_override_seam_lets_a_subclass_replace_the_surcharge()
    {
        var tariffs = Substitute.For<ITariffSource>();
        tariffs.RatePerKilogram("STD").Returns(5.00m);
        var peak = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var engine = new TestableRateEngine(tariffs, new FakeClock(peak));

        var quote = engine.Quote(Request);

        const decimal ExpectedSubtotal = 1015.00m;
        Assert.Equal(ExpectedSubtotal + TestableRateEngine.FixedSurcharge, quote.Amount);
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
