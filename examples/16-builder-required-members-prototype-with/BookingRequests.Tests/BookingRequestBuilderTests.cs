using Xunit;

namespace BookingRequests.Tests;

/// <summary>
/// Proves builder parity with the required-member object initializer, and
/// the runtime validation <see cref="BookingRequestBuilder"/> performs in
/// place of the compiler's required-member check.
/// </summary>
public class BookingRequestBuilderTests
{
    [Fact]
    public void Builder_produces_a_request_equal_to_the_object_initializer_path()
    {
        var expected = new BookingRequest
        {
            Origin = "Seattle, WA",
            Destination = "Portland, OR",
            DeclaredValue = Money.Of(350.00m),
        };

        var built = new BookingRequestBuilder()
            .WithOrigin("Seattle, WA")
            .WithDestination("Portland, OR")
            .WithDeclaredValue(Money.Of(350.00m))
            .Build();

        Assert.Equal(expected, built);
    }

    [Fact]
    public void Build_without_an_origin_throws_booking_validation_exception()
    {
        var builder = new BookingRequestBuilder()
            .WithDestination("Portland, OR")
            .WithDeclaredValue(Money.Of(350.00m));

        Assert.Throws<BookingValidationException>(() => builder.Build());
    }
}
