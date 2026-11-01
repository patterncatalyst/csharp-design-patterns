using Xunit;

namespace ThreadSafeTariffs.Tests;

/// <summary>
/// Proves <see cref="TariffTable"/> itself, independent of which creation
/// strategy published it, hands back the rate it was seeded with.
/// </summary>
public class TariffTableTests
{
    [Fact]
    public void Tariff_table_returns_the_expected_rate_for_a_lane()
    {
        var table = new StandardTariffBuilder().Build();

        Assert.Equal(2.40m, table.RatePerKilogram("STD"));
    }
}
