using Xunit;

namespace ConsignmentAmendments.Tests;

/// <summary>
/// <c>required</c> shifts the "every member must be set" guarantee from a
/// constructor you write to the object initializer the compiler checks.
/// Setting every required member compiles and constructs normally, which is
/// what this test proves. The compile-time failure mode (<c>CS9035</c>:
/// required member not set) when a required member is omitted cannot be
/// expressed as a passing or failing runtime test, because it is diagnosed
/// before the program ever runs. See the README for the counterexample that
/// would not compile.
/// </summary>
public sealed class RequiredMembersTests
{
    [Fact]
    public void Object_initializer_setting_every_required_member_compiles_and_constructs()
    {
        var consignment = new Consignment
        {
            ConsignmentId = "CNS-4217",
            Route = new Route("Rotterdam", "Hamburg"),
            Weight = new Weight(1250m),
        };

        Assert.Equal("CNS-4217", consignment.ConsignmentId);
        Assert.Equal("Rotterdam", consignment.Route.Origin);
        Assert.Equal("Hamburg", consignment.Route.Destination);
        Assert.Equal(1250m, consignment.Weight.Kilograms);
        Assert.Null(consignment.ReferenceNumber);
        Assert.Equal(Money.Zero, consignment.DeclaredValue);
    }
}
