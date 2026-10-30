using Microsoft.Extensions.DependencyInjection;

namespace TariffTables;

/// <summary>
/// The composition root for tariff-table wiring: the one place that lists how
/// the expensive-to-load <see cref="ITariffSource"/> and the <see cref="TariffTable"/>
/// built from it are shared, using the built-in container's own lifetimes
/// instead of either a hand-rolled <see cref="Lazy{T}"/> or a classic static
/// singleton.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CountingTariffSource"/> and <see cref="TariffTable"/>
    /// as singletons, so the whole application shares one already-loaded
    /// table: the container calls <see cref="ITariffSource.Load"/> exactly
    /// once, the first time anything resolves <see cref="TariffTable"/>, and
    /// every later resolution returns that same instance.
    /// </summary>
    public static IServiceCollection AddTariffTables(this IServiceCollection services)
    {
        services.AddSingleton<ITariffSource, CountingTariffSource>();
        services.AddSingleton(provider => new TariffTable(provider.GetRequiredService<ITariffSource>()));

        return services;
    }
}
