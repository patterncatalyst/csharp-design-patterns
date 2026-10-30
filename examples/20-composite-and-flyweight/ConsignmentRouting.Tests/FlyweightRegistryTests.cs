using Xunit;

namespace ConsignmentRouting.Tests;

/// <summary>
/// Proves the Flyweight shape for both interned types: repeated lookups of
/// the same code return the identical shared instance, different codes
/// return different instances, an unknown code is a fault from
/// <see cref="PortCodeRegistry.Get"/> / <see cref="CommodityCodeRegistry.Get"/>
/// but a routine miss from the <c>TryGet</c> counterpart.
/// </summary>
public class FlyweightRegistryTests
{
    [Fact]
    public void PortCodeRegistry_returns_the_same_instance_on_repeated_lookups()
    {
        var registry = new PortCodeRegistry();

        var first = registry.Get("NLRTM");
        var second = registry.Get("NLRTM");

        Assert.Same(first, second);
    }

    [Fact]
    public void PortCodeRegistry_returns_different_instances_for_different_codes()
    {
        var registry = new PortCodeRegistry();

        var rotterdam = registry.Get("NLRTM");
        var hamburg = registry.Get("DEHAM");

        Assert.NotSame(rotterdam, hamburg);
    }

    [Fact]
    public void PortCodeRegistry_Get_throws_for_an_unknown_code()
    {
        var registry = new PortCodeRegistry();

        Assert.Throws<UnknownPortCodeException>(() => registry.Get("ZZZZZ"));
    }

    [Fact]
    public void PortCodeRegistry_TryGet_distinguishes_a_known_code_from_an_unknown_one()
    {
        var registry = new PortCodeRegistry();

        var knownFound = registry.TryGet("CNSHA", out var shanghai);
        var unknownFound = registry.TryGet("ZZZZZ", out var missing);

        Assert.True(knownFound);
        Assert.NotNull(shanghai);
        Assert.False(unknownFound);
        Assert.Null(missing);
    }

    [Fact]
    public void CommodityCodeRegistry_returns_the_same_instance_on_repeated_lookups()
    {
        var registry = new CommodityCodeRegistry();

        var first = registry.Get("8471");
        var second = registry.Get("8471");

        Assert.Same(first, second);
    }

    [Fact]
    public void CommodityCodeRegistry_returns_different_instances_for_different_codes()
    {
        var registry = new CommodityCodeRegistry();

        var dataProcessingMachines = registry.Get("8471");
        var coffee = registry.Get("0901");

        Assert.NotSame(dataProcessingMachines, coffee);
    }

    [Fact]
    public void CommodityCodeRegistry_Get_throws_for_an_unknown_code()
    {
        var registry = new CommodityCodeRegistry();

        Assert.Throws<UnknownCommodityCodeException>(() => registry.Get("9999"));
    }

    [Fact]
    public void CommodityCodeRegistry_TryGet_distinguishes_a_known_code_from_an_unknown_one()
    {
        var registry = new CommodityCodeRegistry();

        var knownFound = registry.TryGet("2709", out var crudeOil);
        var unknownFound = registry.TryGet("9999", out var missing);

        Assert.True(knownFound);
        Assert.NotNull(crudeOil);
        Assert.False(unknownFound);
        Assert.Null(missing);
    }
}
