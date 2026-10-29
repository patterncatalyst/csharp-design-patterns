using Xunit;

namespace Consignments.Tests;

public class ConsignmentTests
{
    private static Consignment BookedConsignment() =>
        new(new ConsignmentId("CN-1001"), "Rotterdam", new Weight(820m));

    [Fact]
    public void Two_consignments_with_the_same_values_are_equal()
    {
        var first = BookedConsignment();
        var second = new Consignment(new ConsignmentId("CN-1001"), "Rotterdam", new Weight(820m));

        Assert.Equal(first, second);
        Assert.True(first == second);
    }

    [Fact]
    public void Consignments_with_different_weights_are_not_equal()
    {
        var first = BookedConsignment();
        var heavier = new Consignment(new ConsignmentId("CN-1001"), "Rotterdam", new Weight(950m));

        Assert.NotEqual(first, heavier);
        Assert.False(first == heavier);
    }

    [Fact]
    public void With_expression_amends_only_the_named_property()
    {
        var original = BookedConsignment();

        var reweighed = original with { Weight = new Weight(905m) };

        Assert.Equal(new Weight(905m), reweighed.Weight);
        Assert.Equal(original.Id, reweighed.Id);
        Assert.Equal(original.Destination, reweighed.Destination);
        Assert.Equal(new Weight(820m), original.Weight);
        Assert.NotEqual(original, reweighed);
    }

    [Fact]
    public void Constructor_rejects_a_blank_destination()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Consignment(new ConsignmentId("CN-1001"), "   ", new Weight(820m)));

        Assert.Equal("Destination", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_non_positive_weight()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(0m));

        Assert.Equal("Kilograms", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_blank_consignment_id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ConsignmentId(""));

        Assert.Equal("Value", exception.ParamName);
    }
}
