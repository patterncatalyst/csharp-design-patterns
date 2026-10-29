using Xunit;

namespace FreightValueTypes.Tests;

/// <summary>
/// The validating constructors on <see cref="Money"/>, <see cref="Weight"/>,
/// and <see cref="SlotId"/> reject the values that would make the shipping
/// domain nonsensical: negative money, negative weight, negative bay or
/// tier. Each throws from the explicit property initializer that shadows
/// its primary-constructor parameter.
/// </summary>
public sealed class ValidationTests
{
    [Fact]
    public void Money_constructor_rejects_negative_amount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-0.01m, "USD"));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDX")]
    public void Money_constructor_rejects_currency_code_of_wrong_length(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, currency));
    }

    [Fact]
    public void Weight_constructor_rejects_negative_kilograms()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(-1m));
    }

    [Fact]
    public void SlotId_constructor_rejects_negative_bay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlotId(-1, 2));
    }

    [Fact]
    public void SlotId_constructor_rejects_negative_tier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlotId(3, -1));
    }

    [Fact]
    public void Money_zero_is_a_valid_benign_default()
    {
        Assert.Equal(0m, Money.Zero.Amount);
        Assert.Equal("USD", Money.Zero.Currency);
    }
}
