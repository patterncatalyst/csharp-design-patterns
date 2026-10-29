using NSubstitute;
using Xunit;

namespace CarrierGateways.Tests;

/// <summary>
/// Proves the Interface Segregation payoff: <see cref="ShipmentRateAdvisor"/>
/// depends only on <see cref="ICarrierRates"/>, so it can be tested against a
/// substitute of that narrow interface alone. There is no need to implement
/// or stub <see cref="ICarrierTracking"/>'s booking and status members just
/// to exercise rate-quoting behavior.
/// </summary>
public class ShipmentRateAdvisorTests
{
    [Fact]
    public async Task Describes_a_quote_from_only_the_narrow_rates_interface()
    {
        var rates = Substitute.For<ICarrierRates>();
        rates.GetRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
            .Returns(new RateQuote(new Money(76.00m), TimeSpan.FromDays(5)));

        var advisor = new ShipmentRateAdvisor(rates);

        var description = await advisor.DescribeQuoteAsync("Rotterdam", 820m, TestContext.Current.CancellationToken);

        Assert.Equal("Rotterdam: 76.00 USD, 5-day transit", description);
        await rates.Received(1).GetRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Passes_the_caller_supplied_cancellation_token_through()
    {
        var rates = Substitute.For<ICarrierRates>();
        rates.GetRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(new RateQuote(new Money(10m), TimeSpan.FromDays(1)));

        var advisor = new ShipmentRateAdvisor(rates);
        using var cancellationSource = new CancellationTokenSource();

        await advisor.DescribeQuoteAsync("Santos", 50m, cancellationSource.Token);

        await rates.Received(1).GetRateAsync("Santos", 50m, cancellationSource.Token);
    }
}
