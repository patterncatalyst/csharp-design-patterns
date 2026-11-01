using NSubstitute;
using Xunit;

namespace RateEngineDependencies.Tests;

/// <summary>
/// Drives the refactored, dependency-broken <see cref="RateEngine"/> through
/// NSubstitute fakes for each of its injected seams: <see cref="ITariffBook"/>,
/// <see cref="IAuditLog"/>, <see cref="IClock"/>, and
/// <see cref="IReferenceGenerator"/>.
/// </summary>
public class DependencyBreakingTests
{
    private static RateEngine BuildEngine(
        out ITariffBook tariffs,
        out IAuditLog audit,
        out IClock clock,
        out IReferenceGenerator references)
    {
        tariffs = Substitute.For<ITariffBook>();
        audit = Substitute.For<IAuditLog>();
        clock = Substitute.For<IClock>();
        references = Substitute.For<IReferenceGenerator>();
        return new RateEngine(tariffs, audit, clock, references);
    }

    [Fact]
    public void Quote_uses_the_injected_tariff_book_rate()
    {
        var engine = BuildEngine(out var tariffs, out _, out var clock, out var references);
        tariffs.RateFor("ROT-NYC").Returns(4.25m);
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        references.NewReference().Returns("RQ-0001");
        var request = new RateRequest("C-9001", 10_000m, 100m, "ROT-NYC");

        var quote = engine.Quote(request, "ops.lead");

        Assert.Equal(425.00m, quote.Amount);
    }

    [Fact]
    public void Quote_records_an_audit_entry_for_the_quote()
    {
        var engine = BuildEngine(out var tariffs, out var audit, out var clock, out var references);
        tariffs.RateFor("SIN-LAX").Returns(5.10m);
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        references.NewReference().Returns("RQ-0002");
        var request = new RateRequest("C-9002", 5_000m, 40m, "SIN-LAX");

        engine.Quote(request, "ops.lead");

        audit.Received(1).Record(Arg.Is<string>(message =>
            message.Contains("RQ-0002", StringComparison.Ordinal) &&
            message.Contains("C-9002", StringComparison.Ordinal)));
    }

    [Fact]
    public void Quote_stamps_the_injected_clock_time()
    {
        var engine = BuildEngine(out var tariffs, out _, out var clock, out var references);
        tariffs.RateFor("HKG-FRA").Returns(6.75m);
        var fixedInstant = new DateTimeOffset(2026, 3, 3, 12, 30, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(fixedInstant);
        references.NewReference().Returns("RQ-0003");
        var request = new RateRequest("C-9003", 2_000m, 20m, "HKG-FRA");

        var quote = engine.Quote(request, "ops.lead");

        Assert.Equal(fixedInstant, quote.QuotedAt);
    }

    [Fact]
    public void Quote_uses_the_injected_reference_generator()
    {
        var engine = BuildEngine(out var tariffs, out _, out var clock, out var references);
        tariffs.RateFor("ROT-NYC").Returns(4.25m);
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        references.NewReference().Returns("RQ-FROM-GENERATOR");
        var request = new RateRequest("C-9004", 1_000m, 10m, "ROT-NYC");

        var quote = engine.Quote(request, "ops.lead");

        Assert.Equal("RQ-FROM-GENERATOR", quote.Reference);
    }

    [Fact]
    public void Quote_carries_the_parameterized_quoted_by_value()
    {
        var engine = BuildEngine(out var tariffs, out _, out var clock, out var references);
        tariffs.RateFor("ROT-NYC").Returns(4.25m);
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        references.NewReference().Returns("RQ-0005");
        var request = new RateRequest("C-9005", 1_000m, 10m, "ROT-NYC");

        var quote = engine.Quote(request, "rate.operator");

        Assert.Equal("rate.operator", quote.QuotedBy);
    }

    [Fact]
    public void Quote_rejects_an_unknown_lane()
    {
        var engine = BuildEngine(out _, out _, out _, out _);
        var request = new RateRequest("C-9006", 1_000m, 10m, "XXX-UNKNOWN");

        Assert.Throws<UnknownLaneException>(() => engine.Quote(request, "ops.lead"));
    }

    [Theory]
    [InlineData("ROT-NYC", true)]
    [InlineData("SIN-LAX", true)]
    [InlineData("HKG-FRA", true)]
    [InlineData("XXX-UNKNOWN", false)]
    [InlineData("", false)]
    public void Lane_validation_is_reachable_through_internals_visible_to(string laneCode, bool expectedKnown)
    {
        Assert.Equal(expectedKnown, LaneValidation.IsKnownLane(laneCode));
    }

    [Fact]
    public void Extract_and_override_seam_replaces_the_reference_generator()
    {
        var tariffs = Substitute.For<ITariffBook>();
        tariffs.RateFor("ROT-NYC").Returns(4.25m);
        var audit = Substitute.For<IAuditLog>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        var references = Substitute.For<IReferenceGenerator>();
        references.NewReference().Returns("RQ-FROM-INJECTED-GENERATOR");
        var engine = new FixedReferenceEngine(tariffs, audit, clock, references);
        var request = new RateRequest("C-9008", 1_000m, 10m, "ROT-NYC");

        var quote = engine.Quote(request, "ops.lead");

        Assert.Equal("RQ-FIXED-0001", quote.Reference);
    }
}
