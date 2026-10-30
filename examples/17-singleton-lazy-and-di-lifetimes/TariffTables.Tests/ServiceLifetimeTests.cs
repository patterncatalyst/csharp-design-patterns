using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TariffTables.Tests;

/// <summary>
/// Proves the Tier 3 shape: the built-in DI container's lifetimes give the
/// same single-shared-instance guarantee as <see cref="ClassicTariffTable"/>
/// and <see cref="LazyTariffTable"/>, with no static access point and no
/// hand-written synchronization, plus the lifetimes a container adds beyond
/// Singleton: Transient (a new instance every resolve) and Scoped (one
/// instance per scope). It also proves the container catches the classic
/// lifetime mistake a Singleton invites: a singleton that captures a scoped
/// dependency.
/// </summary>
public class ServiceLifetimeTests
{
    [Fact]
    public void AddTariffTables_resolves_the_same_TariffTable_instance_every_time()
    {
        var services = new ServiceCollection().AddTariffTables();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<TariffTable>();
        var second = provider.GetRequiredService<TariffTable>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddTariffTables_loads_the_source_exactly_once_across_resolutions()
    {
        var services = new ServiceCollection().AddTariffTables();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<TariffTable>();
        _ = provider.GetRequiredService<TariffTable>();
        _ = provider.GetRequiredService<TariffTable>();

        var source = (CountingTariffSource)provider.GetRequiredService<ITariffSource>();
        Assert.Equal(1, source.LoadCount);
    }

    [Fact]
    public void A_transient_registration_returns_a_different_instance_each_resolve()
    {
        var services = new ServiceCollection();
        services.AddTransient<CountingTariffSource>();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<CountingTariffSource>();
        var second = provider.GetRequiredService<CountingTariffSource>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void A_scoped_registration_returns_the_same_instance_within_one_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<CountingTariffSource>();
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<CountingTariffSource>();
        var second = scope.ServiceProvider.GetRequiredService<CountingTariffSource>();

        Assert.Same(first, second);
    }

    [Fact]
    public void A_scoped_registration_returns_a_different_instance_across_scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<CountingTariffSource>();
        using var provider = services.BuildServiceProvider();

        using var firstScope = provider.CreateScope();
        var fromFirstScope = firstScope.ServiceProvider.GetRequiredService<CountingTariffSource>();

        using var secondScope = provider.CreateScope();
        var fromSecondScope = secondScope.ServiceProvider.GetRequiredService<CountingTariffSource>();

        Assert.NotSame(fromFirstScope, fromSecondScope);
    }

    [Fact]
    public void A_singleton_depending_on_a_scoped_service_throws_when_resolved_from_the_root_provider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITariffSource, CountingTariffSource>();
        services.AddSingleton<LazyTariffTable>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<LazyTariffTable>());
    }
}
