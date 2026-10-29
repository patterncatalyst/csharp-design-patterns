using Xunit;

namespace ConsignmentAmendments.Tests;

/// <summary>
/// Nullable reference types draw the line between a member that is
/// legitimately absent (<see cref="Consignment.ReferenceNumber"/>) and
/// members that must always hold a value (every other property on
/// <see cref="Consignment"/>). These tests exercise the observable behavior
/// on either side of that line, including across a <c>with</c>-based
/// amendment.
/// </summary>
public sealed class NullabilityTests
{
    private static Consignment NewConsignment() => new()
    {
        ConsignmentId = "CNS-4217",
        Route = new Route("Rotterdam", "Hamburg"),
        Weight = new Weight(1250m),
    };

    [Fact]
    public void ReferenceNumber_is_null_on_a_freshly_constructed_consignment()
    {
        var consignment = NewConsignment();

        Assert.Null(consignment.ReferenceNumber);
    }

    [Fact]
    public void ReferenceNumber_is_non_null_after_an_amendment_that_adds_it()
    {
        var consignment = NewConsignment().WithReferenceNumber("PO-9981");

        Assert.NotNull(consignment.ReferenceNumber);
        Assert.Equal("PO-9981", consignment.ReferenceNumber);
    }

    [Fact]
    public void Required_non_nullable_members_never_turn_null_across_a_with_expression()
    {
        var consignment = NewConsignment()
            .ReRouteTo("Antwerp")
            .WithCorrectedWeight(new Weight(1300m));

        Assert.NotNull(consignment.ConsignmentId);
        Assert.NotNull(consignment.Route);
        Assert.Equal("Antwerp", consignment.Route.Destination);
        Assert.Equal(1300m, consignment.Weight.Kilograms);
    }
}
