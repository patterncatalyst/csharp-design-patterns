using NSubstitute;
using Xunit;

namespace RatingEngine.Tests;

/// <summary>
/// NSubstitute's async idiom: <c>.Returns(value)</c> on a
/// <see cref="Task{TResult}"/>-returning member wraps the value in an
/// already-completed task, so the substitute awaits exactly like the real
/// implementation would. Verifying an awaited call needs <c>await</c> in front
/// of <c>Received(...)</c> too, and every token-taking call threads
/// <see cref="TestContext.Current"/>'s cancellation token (or a caller-owned
/// one) through rather than leaving it implicit.
/// </summary>
public class AsyncSubstituteTests
{
    [Fact]
    public async Task An_async_substitute_method_returns_a_completed_task_wrapping_the_stubbed_value()
    {
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
            .Returns(new Money(76.00m));

        var baseRate = await carrierRates.GetBaseRateAsync(
            "Rotterdam", 820m, TestContext.Current.CancellationToken);

        Assert.Equal(new Money(76.00m), baseRate);
    }

    [Fact]
    public async Task Verifying_an_awaited_call_needs_an_await_in_front_of_received()
    {
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(new Money(40.00m));
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(40.00m)).Returns(new Money(44.00m));
        var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
        var consignment = new Consignment("C-2001", "Busan", 640m);

        await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        await carrierRates.Received(1).GetBaseRateAsync("Busan", 640m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Passes_the_callers_cancellation_token_through_every_await()
    {
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(new Money(15.00m));
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(15.00m)).Returns(new Money(15.00m));
        var auditSink = Substitute.For<IAuditSink>();
        var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
        using var cancellationSource = new CancellationTokenSource();
        var consignment = new Consignment("C-2002", "Felixstowe", 12m);

        await engine.QuoteAsync(consignment, cancellationSource.Token);

        await carrierRates.Received(1).GetBaseRateAsync("Felixstowe", 12m, cancellationSource.Token);
        await auditSink.Received(1).RecordAsync("C-2002", new Money(15.00m), cancellationSource.Token);
    }
}
