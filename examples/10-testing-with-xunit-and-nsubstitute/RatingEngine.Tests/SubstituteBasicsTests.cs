using NSubstitute;
using Xunit;

namespace RatingEngine.Tests;

/// <summary>
/// The most basic NSubstitute idiom: <c>Substitute.For&lt;T&gt;()</c> creates a
/// dynamic double of an interface, and <c>.Returns(...)</c> stubs a specific
/// call's return value. No base class, no recorded "expectation" set up ahead
/// of time, no <c>.Object</c> indirection.
/// </summary>
public class SubstituteBasicsTests
{
    [Fact]
    public void A_substituted_tax_policy_returns_the_stubbed_value_for_the_call_it_was_told_about()
    {
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(1.00m)).Returns(new Money(999.99m));

        var result = taxPolicy.ApplyTax(new Money(1.00m));

        Assert.Equal(new Money(999.99m), result);
    }

    [Fact]
    public void A_substituted_tax_policy_can_stub_different_returns_for_different_arguments()
    {
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
        taxPolicy.ApplyTax(new Money(200.00m)).Returns(new Money(220.00m));

        Assert.Equal(new Money(110.00m), taxPolicy.ApplyTax(new Money(100.00m)));
        Assert.Equal(new Money(220.00m), taxPolicy.ApplyTax(new Money(200.00m)));
    }

    [Fact]
    public void An_unstubbed_call_on_a_substitute_returns_the_types_default_value()
    {
        var taxPolicy = Substitute.For<ITaxPolicy>();

        var result = taxPolicy.ApplyTax(new Money(50.00m));

        Assert.Equal(default, result);
    }
}
