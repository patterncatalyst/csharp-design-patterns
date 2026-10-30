using Xunit;

namespace TariffTables.Tests;

/// <summary>
/// Proves the Tier 1 baseline: <see cref="ClassicTariffTable.Instance"/>
/// always returns the same object, the classic Singleton guarantee, and that
/// instance answers lookups like any other <see cref="TariffTable"/>-backed
/// type.
/// </summary>
public class ClassicTariffTableTests
{
    [Fact]
    public void Instance_is_reference_equal_across_calls()
    {
        var first = ClassicTariffTable.Instance;
        var second = ClassicTariffTable.Instance;

        Assert.Same(first, second);
    }

    [Fact]
    public void Instance_looks_up_a_published_lane()
    {
        var ratePerKilogram = ClassicTariffTable.Instance.Lookup("Rotterdam", "Santos");

        Assert.Equal(4.00m, ratePerKilogram);
    }
}
