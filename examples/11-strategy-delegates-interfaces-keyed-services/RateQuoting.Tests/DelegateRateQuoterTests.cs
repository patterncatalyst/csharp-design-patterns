using Xunit;

namespace RateQuoting.Tests;

/// <summary>
/// Proves the Tier 1 dissolve: <see cref="DelegateRateQuoter"/> prices
/// through whatever <see cref="Func{RateRequest, Money}"/> it is given, with
/// no <see cref="IPricingStrategy"/> in the picture.
/// </summary>
public class DelegateRateQuoterTests
{
    [Fact]
    public void Prices_a_request_through_the_supplied_delegate()
    {
        var quoter = new DelegateRateQuoter(request => Money.Of(4.00m * request.WeightKilograms));
        var request = new RateRequest("Rotterdam", 100m, "standard");

        var quote = quoter.Quote(request);

        Assert.Equal(Money.Of(400.00m), quote.Price);
        Assert.Equal("Rotterdam", quote.Destination);
        Assert.Equal("standard", quote.ServiceLevel);
    }

    [Fact]
    public void A_different_delegate_changes_the_quote_from_the_same_quoter_type()
    {
        const decimal surchargePerKilogram = 0.50m;
        var surchargedQuoter = new DelegateRateQuoter(
            request => Money.Of((4.00m + surchargePerKilogram) * request.WeightKilograms));
        var request = new RateRequest("Rotterdam", 100m, "express");

        var quote = surchargedQuoter.Quote(request);

        Assert.Equal(Money.Of(450.00m), quote.Price);
    }

    [Fact]
    public void A_discount_closure_also_changes_the_quote_without_a_new_type()
    {
        const decimal discountPerKilogram = 1.00m;
        var discountedQuoter = new DelegateRateQuoter(
            request => Money.Of((4.00m - discountPerKilogram) * request.WeightKilograms));
        var request = new RateRequest("Santos", 100m, "economy");

        var quote = discountedQuoter.Quote(request);

        Assert.Equal(Money.Of(300.00m), quote.Price);
    }
}
