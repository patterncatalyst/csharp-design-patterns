using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CarrierConfiguration;

/// <summary>
/// The composition root for carrier gateway configuration: the one place that
/// binds <see cref="CarrierGatewayOptions"/> to the <c>CarrierGateway</c>
/// configuration section, wires its validator, enables startup validation,
/// and registers the watcher that observes reloads.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarrierGateway(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<CarrierGatewayOptions>()
            .Bind(configuration.GetSection(CarrierGatewayOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CarrierGatewayOptions>, CarrierGatewayOptionsValidator>();
        services.AddSingleton<RetryBudgetWatcher>();

        return services;
    }

    /// <summary>
    /// Builds a standalone <see cref="IConfigurationRoot"/> from an in-memory
    /// dictionary, with no file, environment, or host involved. Reload the
    /// result by indexing into it (<c>root["Section:Key"] = newValue</c>,
    /// which the in-memory provider supports) and calling <c>Reload()</c>.
    /// </summary>
    public static IConfigurationRoot BuildInMemoryConfiguration(IDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
