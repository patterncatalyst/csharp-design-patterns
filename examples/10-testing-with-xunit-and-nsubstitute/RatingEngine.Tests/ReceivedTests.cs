using NSubstitute;
using Xunit;

namespace RatingEngine.Tests;

/// <summary>
/// Call verification: <c>Received(1)</c>, <c>DidNotReceive()</c>, and the
/// <see cref="Arg"/> matchers (<c>Any</c> and <c>Is</c>) used to check how
/// <see cref="ShipmentRatingEngine"/> drives <see cref="IAuditSink"/>.
/// </summary>
public class ReceivedTests
{
    private static ShipmentRatingEngine BuildEngine(out IAuditSink auditSink, Money baseRate, Money taxedQuote)
    {
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(baseRate);
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(baseRate).Returns(taxedQuote);
        auditSink = Substitute.For<IAuditSink>();
        return new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
    }

    [Fact]
    public async Task Records_exactly_one_audit_entry_for_the_quoted_consignment()
    {
        var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));
        var consignment = new Consignment("C-1001", "Rotterdam", 300m);

        await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        await auditSink.Received(1).RecordAsync("C-1001", new Money(110.00m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_records_any_quote_other_than_the_one_it_returned()
    {
        var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));
        var consignment = new Consignment("C-1002", "Santos", 50m);

        await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        await auditSink.DidNotReceive().RecordAsync(
            Arg.Any<string>(), Arg.Is<Money>(quoted => quoted != new Money(110.00m)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Records_against_a_consignment_id_that_matches_a_predicate()
    {
        var engine = BuildEngine(out var auditSink, new Money(60.00m), new Money(66.00m));
        var consignment = new Consignment("C-SANTOS-7", "Santos", 120m);

        await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        await auditSink.Received(1).RecordAsync(
            Arg.Is<string>(id => id.StartsWith("C-SANTOS", StringComparison.Ordinal)),
            Arg.Any<Money>(),
            Arg.Any<CancellationToken>());
    }
}
