using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarrierConfiguration.Tests;

/// <summary>
/// Proves the three ways to read bound <see cref="CarrierGatewayOptions"/>
/// back out of the container: the long-lived singleton <c>IOptions</c>, the
/// per-scope <c>IOptionsSnapshot</c> that rebinds after a reload, and the
/// always-current <c>IOptionsMonitor</c>.
/// </summary>
public class OptionsBindingTests
{
    private static Dictionary<string, string?> ValidValues() => new()
    {
        [$"{CarrierGatewayOptions.SectionName}:Endpoint"] = "https://gateway.carrier.example",
        [$"{CarrierGatewayOptions.SectionName}:TimeoutSeconds"] = "30",
        [$"{CarrierGatewayOptions.SectionName}:RetryBudget"] = "3",
    };

    [Fact]
    public void Options_binds_endpoint_timeout_and_retry_budget_from_configuration()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CarrierGatewayOptions>>().Value;

        Assert.Equal("https://gateway.carrier.example", options.Endpoint);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal(3, options.RetryBudget);
    }

    [Fact]
    public void IOptions_returns_the_same_singleton_instance()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IOptions<CarrierGatewayOptions>>().Value;
        var second = provider.GetRequiredService<IOptions<CarrierGatewayOptions>>().Value;

        Assert.Same(first, second);
    }

    [Fact]
    public void IOptionsSnapshot_reflects_configuration_within_a_scope()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CarrierGatewayOptions>>().Value;

        Assert.Equal(30, snapshot.TimeoutSeconds);
    }

    [Fact]
    public void IOptionsSnapshot_rebinds_in_a_new_scope_after_a_reload()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        using (var firstScope = provider.CreateScope())
        {
            var before = firstScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CarrierGatewayOptions>>().Value;
            Assert.Equal(3, before.RetryBudget);
        }

        configuration[$"{CarrierGatewayOptions.SectionName}:RetryBudget"] = "7";
        configuration.Reload();

        using var secondScope = provider.CreateScope();
        var after = secondScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CarrierGatewayOptions>>().Value;

        Assert.Equal(7, after.RetryBudget);
    }

    [Fact]
    public void IOptionsMonitor_current_value_reflects_the_bound_configuration()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IOptionsMonitor<CarrierGatewayOptions>>();

        Assert.Equal("https://gateway.carrier.example", monitor.CurrentValue.Endpoint);
    }
}
