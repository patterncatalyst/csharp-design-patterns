using Xunit;

namespace ConsignmentAmendments.Tests;

/// <summary>
/// Structural equality for <see cref="Consignment"/> (a record class) and its
/// nested <see cref="Route"/> (a record) and <see cref="Weight"/> /
/// <see cref="Money"/> (record structs). None of <c>Equals</c>,
/// <c>GetHashCode</c>, or <c>==</c>/<c>!=</c> is hand-written anywhere in
/// this example; all of it comes from the record declarations.
/// </summary>
public sealed class ValueEqualityTests
{
    private static Consignment NewConsignment() => new()
    {
        ConsignmentId = "CNS-4217",
        Route = new Route("Rotterdam", "Hamburg"),
        Weight = new Weight(1250m),
    };

    [Fact]
    public void Two_independently_constructed_consignments_with_identical_data_are_equal()
    {
        var first = NewConsignment();
        var second = NewConsignment();

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Changing_the_nested_route_makes_consignments_unequal()
    {
        var first = NewConsignment();
        var second = first with { Route = new Route("Rotterdam", "Antwerp") };

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void Changing_the_weight_makes_consignments_unequal()
    {
        var first = NewConsignment();
        var second = first with { Weight = new Weight(999m) };

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Route_with_same_origin_and_destination_is_equal_and_shares_hash_code()
    {
        var left = new Route("Rotterdam", "Hamburg");
        var right = new Route("Rotterdam", "Hamburg");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Weight_with_same_kilograms_is_equal_and_shares_hash_code()
    {
        var left = new Weight(1250m);
        var right = new Weight(1250m);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Money_with_same_amount_and_currency_is_equal_and_shares_hash_code()
    {
        var left = new Money(500m, "EUR");
        var right = new Money(500m, "EUR");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}
