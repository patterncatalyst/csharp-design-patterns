using Xunit;

namespace ShipmentWorkflow.Tests;

/// <summary>
/// The rating Strategy and its Factory: <see cref="RatingStrategies.For"/>
/// selects between <see cref="StandardRatingStrategy"/> and
/// <see cref="ExpressRatingStrategy"/>, and the express strategy multiplies
/// the exact same standard base amount rather than recomputing its own.
/// </summary>
public class RatingTests
{
    private static readonly BookingRequest StandardBooking = new(
        "CN-7001", "USLAX", "SGSIN", DeclaredValue: 10_000.00m, WeightKilograms: 500.00m, Service: ServiceLevel.Standard);

    private static readonly BookingRequest ExpressBooking = StandardBooking with { Service = ServiceLevel.Express };

    [Fact]
    public void Express_booking_costs_more_than_the_same_standard_booking()
    {
        var standardQuote = RatingStrategies.For(ServiceLevel.Standard).Quote(StandardBooking);
        var expressQuote = RatingStrategies.For(ServiceLevel.Express).Quote(ExpressBooking);

        Assert.True(expressQuote.Amount > standardQuote.Amount);
    }

    [Fact]
    public void Rating_strategy_is_selected_by_service_level()
    {
        Assert.IsType<StandardRatingStrategy>(RatingStrategies.For(ServiceLevel.Standard));
        Assert.IsType<ExpressRatingStrategy>(RatingStrategies.For(ServiceLevel.Express));
    }

    [Fact]
    public void Standard_rating_computes_the_expected_amount()
    {
        var quote = new StandardRatingStrategy().Quote(StandardBooking);

        Assert.Equal(1_400.00m, quote.Amount);
    }

    [Fact]
    public void Express_rating_applies_the_express_multiplier()
    {
        var standardAmount = new StandardRatingStrategy().Quote(StandardBooking).Amount;

        var expressQuote = new ExpressRatingStrategy().Quote(ExpressBooking);

        Assert.Equal(standardAmount * 1.40m, expressQuote.Amount);
        Assert.Equal(1_960.00m, expressQuote.Amount);
    }
}
