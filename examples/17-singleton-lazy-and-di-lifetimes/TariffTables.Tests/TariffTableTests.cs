using Xunit;

namespace TariffTables.Tests;

/// <summary>
/// Proves <see cref="TariffTable"/> itself: it indexes the loaded entries by
/// lane and either returns the published rate or throws, rather than
/// silently returning zero for an unpublished lane.
/// </summary>
public class TariffTableTests
{
    [Fact]
    public void Looks_up_the_published_rate_for_a_known_lane()
    {
        var table = new TariffTable(new CountingTariffSource());

        var ratePerKilogram = table.Lookup("Rotterdam", "Santos");

        Assert.Equal(4.00m, ratePerKilogram);
    }

    [Fact]
    public void An_unpublished_lane_throws_instead_of_returning_zero()
    {
        var table = new TariffTable(new CountingTariffSource());

        Assert.Throws<KeyNotFoundException>(() => table.Lookup("Rotterdam", "Nowhere"));
    }

    [Fact]
    public void Constructing_from_a_source_loads_it_exactly_once()
    {
        var source = new CountingTariffSource();

        _ = new TariffTable(source);

        Assert.Equal(1, source.LoadCount);
    }
}
