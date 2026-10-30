using Xunit;

namespace RateQuoting.Tests;

/// <summary>
/// Proves the Tier 2 shape: <see cref="StrategyRateQuoter"/> is built,
/// unmodified, against two different <see cref="IPricingStrategy"/>
/// implementations, and only the resolved price differs.
/// </summary>
public class StrategyRateQuoterTests
{
    [Fact]
    public void Standard_and_express_strategies_yield_different_quotes_from_the_unchanged_caller()
    {
        var standardQuoter = new StrategyRateQuoter(new StandardTariff());
        var expressQuoter = new StrategyRateQuoter(new ExpressTariff());

        var standardQuote = standardQuoter.Quote(new RateRequest("Singapore", 100m, "standard"));
        var expressQuote = expressQuoter.Quote(new RateRequest("Singapore", 100m, "express"));

        Assert.Equal(Money.Of(400.00m), standardQuote.Price);
        Assert.Equal(Money.Of(650.00m), expressQuote.Price);
        Assert.NotEqual(standardQuote.Price, expressQuote.Price);
    }

    [Fact]
    public void Economy_strategy_prices_under_standard_from_the_same_caller_type()
    {
        var economyQuoter = new StrategyRateQuoter(new EconomyTariff());

        var economyQuote = economyQuoter.Quote(new RateRequest("Santos", 100m, "economy"));

        Assert.Equal(Money.Of(300.00m), economyQuote.Price);
    }
}
