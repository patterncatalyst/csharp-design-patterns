using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RateQuoting.Tests;

/// <summary>
/// Proves the Tier 3 shape: the built-in DI container's keyed services pick
/// the <see cref="IPricingStrategy"/> by <see cref="RateRequest.ServiceLevel"/>
/// instead of any consumer branching on it, that the fixed-key
/// <see cref="FromKeyedServicesAttribute"/> form resolves the same way, and
/// that the keyed and non-keyed service graphs are genuinely separate.
/// </summary>
public class KeyedTariffQuoterTests
{
    [Fact]
    public void Resolves_the_express_strategy_by_key_and_prices_accordingly()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();

        var strategy = provider.GetRequiredKeyedService<IPricingStrategy>("express");
        var price = strategy.Price(new RateRequest("Santos", 100m, "express"));

        Assert.IsType<ExpressTariff>(strategy);
        Assert.Equal(Money.Of(650.00m), price);
    }

    [Fact]
    public void Dispatches_to_the_standard_tariff_by_service_level()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();
        var quoter = provider.GetRequiredService<KeyedTariffQuoter>();

        var quote = quoter.Quote(new RateRequest("Rotterdam", 100m, "standard"));

        Assert.Equal(Money.Of(400.00m), quote.Price);
    }

    [Fact]
    public void Dispatches_to_the_express_tariff_by_service_level()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();
        var quoter = provider.GetRequiredService<KeyedTariffQuoter>();

        var quote = quoter.Quote(new RateRequest("Rotterdam", 100m, "express"));

        Assert.Equal(Money.Of(650.00m), quote.Price);
    }

    [Fact]
    public void Dispatches_to_the_economy_tariff_by_service_level()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();
        var quoter = provider.GetRequiredService<KeyedTariffQuoter>();

        var quote = quoter.Quote(new RateRequest("Rotterdam", 100m, "economy"));

        Assert.Equal(Money.Of(300.00m), quote.Price);
    }

    [Fact]
    public void FromKeyedServices_consumer_resolves_the_fixed_express_key()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();
        var quoter = provider.GetRequiredService<ExpressLaneQuoter>();

        var quote = quoter.Quote(new RateRequest("Santos", 100m, "standard"));

        Assert.Equal(Money.Of(650.00m), quote.Price);
        Assert.Equal("express", quote.ServiceLevel);
    }

    [Fact]
    public void An_unknown_key_throws_from_GetRequiredKeyedService_but_GetKeyedService_returns_null()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredKeyedService<IPricingStrategy>("overnight"));
        Assert.Null(provider.GetKeyedService<IPricingStrategy>("overnight"));
    }

    [Fact]
    public void A_non_keyed_resolution_returns_null_because_keyed_and_non_keyed_are_separate_graphs()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IPricingStrategy>());
    }

    [Fact]
    public void Swapping_the_requested_key_changes_the_quote_without_editing_the_caller()
    {
        var services = new ServiceCollection().AddRateQuoting();
        using var provider = services.BuildServiceProvider();
        var quoter = provider.GetRequiredService<KeyedTariffQuoter>();

        var standardQuote = quoter.Quote(new RateRequest("Rotterdam", 100m, "standard"));
        var economyQuote = quoter.Quote(new RateRequest("Rotterdam", 100m, "economy"));

        Assert.NotEqual(standardQuote.Price, economyQuote.Price);
    }
}
