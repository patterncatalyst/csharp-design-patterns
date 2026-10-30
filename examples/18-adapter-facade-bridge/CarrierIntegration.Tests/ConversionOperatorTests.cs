using Xunit;

namespace CarrierIntegration.Tests;

/// <summary>
/// Proves the user-defined conversion operators behave the way their
/// implicit/explicit classification promises: the lossless
/// <see cref="Money"/>-to-decimal widening needs no cast, the lossy
/// decimal-to-<see cref="Money"/> narrowing needs one, and each operator's
/// named alternate agrees with the operator.
/// </summary>
public class ConversionOperatorTests
{
    [Fact]
    public void Money_converts_to_decimal_implicitly_and_matches_ToDecimal()
    {
        var money = new Money(12345);

        decimal dollars = money;

        Assert.Equal(123.45m, dollars);
        Assert.Equal(money.ToDecimal(), dollars);
    }

    [Fact]
    public void Decimal_converts_to_Money_explicitly_and_round_trips_through_FromDecimal()
    {
        decimal dollars = 99.07m;

        var money = (Money)dollars;

        Assert.Equal(Money.FromDecimal(dollars), money);
        decimal roundTripped = money;
        Assert.Equal(dollars, roundTripped);
    }

    [Fact]
    public void Kilograms_and_Pounds_convert_explicitly_and_round_trip_within_tolerance()
    {
        var weight = new Kilograms(10m);

        var pounds = (Pounds)weight;
        var backToKilograms = (Kilograms)pounds;

        Assert.Equal(weight.ToPounds(), pounds);
        Assert.Equal(pounds.ToKilograms(), backToKilograms);
        Assert.True(Math.Abs(backToKilograms.Value - weight.Value) < 0.0001m);
    }
}
