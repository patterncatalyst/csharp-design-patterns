using Xunit;

namespace ConsignmentAmendments.Tests;

/// <summary>
/// Each amendment helper on <see cref="Consignment"/> returns a new value
/// produced by a <c>with</c> expression; the receiver is a record class, so
/// <c>with</c> performs a shallow copy rather than mutating any field on the
/// original.
/// </summary>
public sealed class WithAmendmentTests
{
    private static Consignment NewConsignment() => new()
    {
        ConsignmentId = "CNS-4217",
        Route = new Route("Rotterdam", "Hamburg"),
        Weight = new Weight(1250m),
    };

    [Fact]
    public void ReRouteTo_returns_a_new_consignment_and_leaves_the_original_unchanged()
    {
        var original = NewConsignment();

        var amended = original.ReRouteTo("Antwerp");

        Assert.Equal("Hamburg", original.Route.Destination);
        Assert.Equal("Antwerp", amended.Route.Destination);
        Assert.Equal("Rotterdam", amended.Route.Origin);
        Assert.NotEqual(original, amended);
    }

    [Fact]
    public void WithCorrectedWeight_returns_a_new_consignment_and_leaves_the_original_unchanged()
    {
        var original = NewConsignment();

        var amended = original.WithCorrectedWeight(new Weight(1300m));

        Assert.Equal(1250m, original.Weight.Kilograms);
        Assert.Equal(1300m, amended.Weight.Kilograms);
        Assert.NotEqual(original, amended);
    }

    [Fact]
    public void WithReferenceNumber_returns_a_new_consignment_and_leaves_the_original_unchanged()
    {
        var original = NewConsignment();

        var amended = original.WithReferenceNumber("PO-9981");

        Assert.Null(original.ReferenceNumber);
        Assert.Equal("PO-9981", amended.ReferenceNumber);
        Assert.NotEqual(original, amended);
    }

    [Fact]
    public void Declared_value_can_be_amended_with_a_bare_with_expression_at_the_call_site()
    {
        var original = NewConsignment();

        var amended = original with { DeclaredValue = new Money(18_500m, "EUR") };

        Assert.Equal(Money.Zero, original.DeclaredValue);
        Assert.Equal(new Money(18_500m, "EUR"), amended.DeclaredValue);
        Assert.NotEqual(original, amended);
    }

    [Fact]
    public void An_amendment_that_reproduces_the_original_data_compares_equal_to_it()
    {
        var original = NewConsignment();

        var amended = original.ReRouteTo("Hamburg");

        Assert.Equal(original, amended);
        Assert.True(original == amended);
    }
}
