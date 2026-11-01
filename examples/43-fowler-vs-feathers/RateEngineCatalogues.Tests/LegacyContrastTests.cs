using System.Reflection;
using Xunit;

namespace RateEngineCatalogues.Tests;

/// <summary>
/// Proves <see cref="Legacy.LegacyRateEngine"/>'s untestability by structure,
/// through reflection, rather than by asserting on the wall clock (which
/// would be flaky): the type is sealed with a single parameterless
/// constructor, and its <c>Quote</c> method is non-virtual, so neither
/// Feathers' constructor seam nor Fowler's extracted strategy can be applied
/// from outside the class until the seam opens it up.
/// </summary>
public class LegacyContrastTests
{
    [Fact]
    public void Legacy_engine_offers_no_seam_before_feathers_opens_one()
    {
        var legacyEngineType = typeof(Legacy.LegacyRateEngine);

        Assert.True(legacyEngineType.IsSealed);

        var constructors = legacyEngineType.GetConstructors();
        var constructor = Assert.Single(constructors);
        Assert.Empty(constructor.GetParameters());

        var quoteMethod = legacyEngineType.GetMethod("Quote", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(quoteMethod);
        Assert.False(quoteMethod.IsVirtual);
    }
}
