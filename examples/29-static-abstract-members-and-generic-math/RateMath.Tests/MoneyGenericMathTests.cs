using Xunit;

namespace RateMath.Tests;

public class MoneyGenericMathTests
{
    [Fact]
    public void Money_adds_through_the_addition_operator()
    {
        var left = new Money(10.00m);
        var right = new Money(5.25m);

        var sum = left + right;

        Assert.Equal(new Money(15.25m), sum);
    }

    [Fact]
    public void Money_additive_identity_is_zero()
    {
        Assert.Equal(new Money(0m), Money.AdditiveIdentity);
    }

    [Fact]
    public void Money_scales_by_a_decimal_factor()
    {
        var unitRate = new Money(10.00m);

        var scaled = unitRate * 3m;

        Assert.Equal(new Money(30.00m), scaled);
    }

    [Theory]
    [InlineData(5, 10, true, true, false, false)]
    [InlineData(10, 10, false, true, false, true)]
    [InlineData(10, 5, false, false, true, true)]
    public void Money_comparison_operators_order_amounts(
        decimal leftAmount,
        decimal rightAmount,
        bool expectedLessThan,
        bool expectedLessThanOrEqual,
        bool expectedGreaterThan,
        bool expectedGreaterThanOrEqual)
    {
        var left = new Money(leftAmount);
        var right = new Money(rightAmount);

        Assert.Equal(expectedLessThan, left < right);
        Assert.Equal(expectedLessThanOrEqual, left <= right);
        Assert.Equal(expectedGreaterThan, left > right);
        Assert.Equal(expectedGreaterThanOrEqual, left >= right);
    }

    [Fact]
    public void Sum_totals_a_sequence_of_money()
    {
        var charges = new[] { new Money(1.50m), new Money(2.25m), new Money(0.75m) };

        var total = Totals.Sum(charges);

        Assert.Equal(new Money(4.50m), total);
    }

    [Fact]
    public void Sum_totals_a_sequence_of_decimals()
    {
        // Same generic Totals.Sum<T>, instantiated over the built-in decimal
        // (which implements INumber<decimal>, so it satisfies the
        // IAdditionOperators/IAdditiveIdentity constraint) to prove the
        // algorithm is not Money-specific.
        var amounts = new decimal[] { 1.5m, 2.25m, 0.75m };

        var total = Totals.Sum(amounts);

        Assert.Equal(4.50m, total);
    }
}
