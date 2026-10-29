using Xunit;

namespace RateCalculation.Tests;

/// <summary>SRP: the tax policy is tested with no pricing or audit collaborator in sight.</summary>
public class PercentageTaxPolicyTests
{
    [Fact]
    public void Adds_the_configured_percentage_on_top_of_the_subtotal()
    {
        var policy = new PercentageTaxPolicy(taxRate: 0.08m);

        var total = policy.ApplyTax(new Money(100.00m, "USD"));

        Assert.Equal(new Money(108.00m, "USD"), total);
    }

    [Fact]
    public void A_zero_rate_leaves_the_subtotal_unchanged()
    {
        var policy = new PercentageTaxPolicy(taxRate: 0m);

        var total = policy.ApplyTax(new Money(42.00m, "USD"));

        Assert.Equal(new Money(42.00m, "USD"), total);
    }

    [Fact]
    public void Rejects_a_negative_tax_rate()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PercentageTaxPolicy(taxRate: -0.01m));

        Assert.Equal("taxRate", exception.ParamName);
    }
}
