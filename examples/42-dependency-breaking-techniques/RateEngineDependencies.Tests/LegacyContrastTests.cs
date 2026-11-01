using System.Reflection;
using Xunit;

namespace RateEngineDependencies.Tests;

/// <summary>
/// Proves the legacy engine's untestability structurally, through
/// reflection, rather than by asserting on an ambient value (the clock,
/// reference, or caller it bakes in, none of which a test can pin down
/// without a seam). Also exercises the production implementations of the
/// refactored seams.
/// </summary>
public class LegacyContrastTests
{
    [Fact]
    public void Legacy_engine_bakes_in_its_dependencies_with_no_seam()
    {
        var engineType = typeof(RateEngineDependencies.Legacy.RateEngine);

        Assert.True(engineType.IsSealed);

        var parameterlessConstructor = engineType.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(parameterlessConstructor);

        var quoteMethod = engineType.GetMethod(
            nameof(RateEngineDependencies.Legacy.RateEngine.Quote),
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(quoteMethod);
        Assert.False(quoteMethod!.IsVirtual);

        // A C# static class compiles to an abstract sealed type in the CLR;
        // this is how reflection confirms LegacyTariffBook has no instance
        // seam at all, not even a constructor to not call.
        var tariffBookType = typeof(RateEngineDependencies.Legacy.LegacyTariffBook);
        Assert.True(tariffBookType is { IsAbstract: true, IsSealed: true });
    }

    [Fact]
    public void Standard_tariff_book_and_guid_reference_generator_behave()
    {
        var tariffs = new StandardTariffBook();

        Assert.Equal(4.25m, tariffs.RateFor("ROT-NYC"));
        Assert.Equal(5.10m, tariffs.RateFor("SIN-LAX"));
        Assert.Equal(6.75m, tariffs.RateFor("HKG-FRA"));

        var references = new GuidReferenceGenerator();
        var reference = references.NewReference();

        Assert.Equal(32, reference.Length);
        Assert.Matches("^[0-9a-f]{32}$", reference);
    }
}
