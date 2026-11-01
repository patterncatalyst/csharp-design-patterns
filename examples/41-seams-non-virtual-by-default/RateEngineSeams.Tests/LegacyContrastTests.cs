using System.Reflection;
using Xunit;

namespace RateEngineSeams.Tests;

/// <summary>
/// Proves the legacy <see cref="Legacy.RateEngine"/>'s untestability by
/// structure, through reflection, rather than by asserting on the wall clock
/// (which would be flaky): the type is sealed with a single parameterless
/// constructor, and its <c>Quote</c> method is non-virtual, so nothing about
/// either collaborator can be substituted from outside the class.
/// </summary>
public class LegacyContrastTests
{
    [Fact]
    public void Legacy_engine_offers_no_seam_for_the_clock_or_the_tariff_source()
    {
        var legacyEngineType = typeof(Legacy.RateEngine);

        Assert.True(legacyEngineType.IsSealed);

        var constructors = legacyEngineType.GetConstructors();
        var constructor = Assert.Single(constructors);
        Assert.Empty(constructor.GetParameters());

        var quoteMethod = legacyEngineType.GetMethod("Quote", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(quoteMethod);
        Assert.False(quoteMethod.IsVirtual);
    }
}
