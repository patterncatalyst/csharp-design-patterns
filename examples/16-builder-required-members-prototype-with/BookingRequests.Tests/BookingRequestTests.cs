using Xunit;

namespace BookingRequests.Tests;

/// <summary>
/// Proves object-initializer construction: a <see cref="BookingRequest"/>
/// built with all three required members set yields the expected values,
/// with every init-optional member defaulting as declared.
/// </summary>
public class BookingRequestTests
{
    [Fact]
    public void Object_initializer_with_required_members_yields_the_expected_request()
    {
        var request = new BookingRequest
        {
            Origin = "Chicago, IL",
            Destination = "Denver, CO",
            DeclaredValue = Money.Of(500.00m),
        };

        Assert.Equal("Chicago, IL", request.Origin);
        Assert.Equal("Denver, CO", request.Destination);
        Assert.Equal(Money.Of(500.00m), request.DeclaredValue);
        Assert.Equal("standard", request.ServiceLevel);
        Assert.Empty(request.LineItems);
        Assert.False(request.InsuranceRequested);
        Assert.Null(request.SpecialInstructions);
        Assert.Null(request.CustomerReference);
    }
}
