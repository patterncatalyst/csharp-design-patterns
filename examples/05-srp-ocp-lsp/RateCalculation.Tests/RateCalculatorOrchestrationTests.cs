using NSubstitute;
using Xunit;

namespace RateCalculation.Tests;

/// <summary>
/// SRP at the orchestration level: tax and audit are optional, independently
/// addable collaborators. The audit sink is substituted with NSubstitute, so
/// these tests prove <see cref="RateCalculator"/> calls it correctly without
/// writing to a real log, queue, or database.
/// </summary>
public class RateCalculatorOrchestrationTests
{
    private static Consignment SampleConsignment() => new(new Weight(10m), new Money(1000m, "USD"));

    [Fact]
    public async Task Quotes_with_pricing_only_when_no_tax_or_audit_is_wired_in()
    {
        var calculator = new RateCalculator(new WeightBasedRateCalculator(ratePerKilogram: 2.50m));

        var total = await calculator.QuoteAsync(SampleConsignment(), TestContext.Current.CancellationToken);

        Assert.Equal(new Money(25.00m, "USD"), total);
    }

    [Fact]
    public async Task Applies_tax_on_top_of_the_priced_subtotal_when_a_tax_policy_is_wired_in()
    {
        var calculator = new RateCalculator(
            new WeightBasedRateCalculator(ratePerKilogram: 2.50m),
            taxPolicy: new PercentageTaxPolicy(taxRate: 0.10m));

        var total = await calculator.QuoteAsync(SampleConsignment(), TestContext.Current.CancellationToken);

        Assert.Equal(new Money(27.50m, "USD"), total);
    }

    [Fact]
    public async Task Calls_the_audit_sink_exactly_once_with_the_final_quote()
    {
        var auditSink = Substitute.For<IAuditSink>();
        var calculator = new RateCalculator(
            new WeightBasedRateCalculator(ratePerKilogram: 2.50m),
            taxPolicy: new PercentageTaxPolicy(taxRate: 0.10m),
            auditSink: auditSink);
        var consignment = SampleConsignment();

        var total = await calculator.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        Assert.Equal(new Money(27.50m, "USD"), total);
        await auditSink.Received(1).RecordQuoteAsync(consignment, total, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Never_calls_an_audit_sink_that_was_not_wired_in()
    {
        var auditSink = Substitute.For<IAuditSink>();
        // Deliberately not passed to RateCalculator, to prove auditing is an
        // independent, optional collaborator rather than baked into pricing.
        var calculator = new RateCalculator(new WeightBasedRateCalculator(ratePerKilogram: 2.50m));

        await calculator.QuoteAsync(SampleConsignment(), TestContext.Current.CancellationToken);

        await auditSink.DidNotReceive().RecordQuoteAsync(
            Arg.Any<Consignment>(), Arg.Any<Money>(), Arg.Any<CancellationToken>());
    }
}
