using System.Reflection;
using Xunit;

namespace RateEngineReplacement.Tests;

/// <summary>
/// Proves the legacy <see cref="Legacy.LegacyRateEngine"/> has no stable seam
/// to refactor or rewrite behind, through reflection rather than by asserting
/// on the wall clock (which would be flaky): the type is sealed with a single
/// parameterless constructor, its <c>Quote</c> method is non-virtual, and the
/// type is not assignable to <see cref="IRateEngine"/>, so neither
/// replacement engine can stand in for it without a source change at every
/// call site.
/// </summary>
public class LegacyContrastTests
{
    [Fact]
    public void Legacy_engine_has_no_stable_seam_to_refactor_or_rewrite_behind()
    {
        var legacyEngineType = typeof(Legacy.LegacyRateEngine);

        Assert.True(legacyEngineType.IsSealed);

        var constructors = legacyEngineType.GetConstructors();
        var constructor = Assert.Single(constructors);
        Assert.Empty(constructor.GetParameters());

        var quoteMethod = legacyEngineType.GetMethod("Quote", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(quoteMethod);
        Assert.False(quoteMethod.IsVirtual);

        Assert.False(typeof(IRateEngine).IsAssignableFrom(legacyEngineType));
    }
}
