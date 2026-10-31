using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarrierConfiguration.Tests;

/// <summary>
/// Proves live reload through <c>IOptionsMonitor.OnChange</c> and
/// <see cref="RetryBudgetWatcher"/>, the validator's reason-collecting
/// behavior, and that <c>ValidateOnStart</c> actually stops a misconfigured
/// deployment instead of only logging a warning.
/// </summary>
public class OptionsChangeAndValidationTests
{
    private static Dictionary<string, string?> ValidValues() => new()
    {
        [$"{CarrierGatewayOptions.SectionName}:Endpoint"] = "https://gateway.carrier.example",
        [$"{CarrierGatewayOptions.SectionName}:TimeoutSeconds"] = "30",
        [$"{CarrierGatewayOptions.SectionName}:RetryBudget"] = "3",
    };

    [Fact]
    public void IOptionsMonitor_OnChange_fires_with_the_new_value_after_a_reload()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        var watcher = provider.GetRequiredService<RetryBudgetWatcher>();

        configuration[$"{CarrierGatewayOptions.SectionName}:RetryBudget"] = "9";
        configuration.Reload();

        Assert.Contains(watcher.Observed, observed => observed.RetryBudget == 9);
        Assert.Equal(9, watcher.CurrentRetryBudget);
    }

    [Fact]
    public void Validator_accepts_a_well_formed_configuration()
    {
        var validator = new CarrierGatewayOptionsValidator();
        var options = new CarrierGatewayOptions
        {
            Endpoint = "https://gateway.carrier.example",
            TimeoutSeconds = 30,
            RetryBudget = 3,
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    public static TheoryData<CarrierGatewayOptions, string> InvalidConfigurations => new()
    {
        {
            new CarrierGatewayOptions { Endpoint = "", TimeoutSeconds = 30, RetryBudget = 3 },
            "blank endpoint"
        },
        {
            new CarrierGatewayOptions { Endpoint = "https://gateway.carrier.example", TimeoutSeconds = 0, RetryBudget = 3 },
            "non-positive timeout"
        },
        {
            new CarrierGatewayOptions { Endpoint = "https://gateway.carrier.example", TimeoutSeconds = 30, RetryBudget = -1 },
            "negative retry budget"
        },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigurations))]
    public void Validator_rejects_invalid_configuration(CarrierGatewayOptions options, string reason)
    {
        var validator = new CarrierGatewayOptionsValidator();

        var result = validator.Validate(name: null, options);

        Assert.True(result.Failed, $"expected validation to fail for {reason}");
        Assert.NotEmpty(result.Failures);
    }

    [Fact]
    public void ValidateOnStart_throws_for_invalid_configuration_at_startup()
    {
        var invalidValues = new Dictionary<string, string?>
        {
            [$"{CarrierGatewayOptions.SectionName}:Endpoint"] = "",
            [$"{CarrierGatewayOptions.SectionName}:TimeoutSeconds"] = "0",
            [$"{CarrierGatewayOptions.SectionName}:RetryBudget"] = "-1",
        };
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(invalidValues);
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        // IOptionsMonitor<T> resolves on this pin, so the IStartupValidator
        // registered by ValidateOnStart() is reachable directly: a real host
        // calls this during startup, before accepting any traffic.
        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        var exception = Assert.Throws<OptionsValidationException>(() => startupValidator.Validate());

        Assert.Contains(exception.Failures, failure => failure.Contains("Endpoint", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("TimeoutSeconds", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("RetryBudget", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateOnStart_passes_for_valid_configuration()
    {
        var configuration = ServiceCollectionExtensions.BuildInMemoryConfiguration(ValidValues());
        var services = new ServiceCollection().AddCarrierGateway(configuration);
        using var provider = services.BuildServiceProvider();

        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        var exception = Record.Exception(() => startupValidator.Validate());

        Assert.Null(exception);
    }
}
