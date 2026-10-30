using NSubstitute;
using Xunit;

namespace RateComposition.Tests;

/// <summary>
/// Proves the caching Proxy shape: a caller holding
/// <see cref="CachingRateGatewayProxy"/> gets the same answers as calling
/// the wrapped <see cref="ICarrierRateGateway"/> directly, but a repeated
/// request for an equal <see cref="RateRequest"/> is answered from the cache
/// instead of reaching the wrapped gateway again.
/// </summary>
public class CachingRateGatewayProxyTests
{
    private static readonly RateRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void A_second_call_with_an_equal_request_returns_the_cached_value_without_calling_the_inner_gateway_again()
    {
        var inner = Substitute.For<ICarrierRateGateway>();
        var expected = new CarrierRate(Money.Of(750.00m), TransitDays: 3);
        inner.GetRate(Request).Returns(expected);
        var proxy = new CachingRateGatewayProxy(inner);

        var first = proxy.GetRate(Request);
        var second = proxy.GetRate(new RateRequest("Rotterdam", "Santos", 100m));

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        inner.Received(1).GetRate(Arg.Any<RateRequest>());
    }

    [Fact]
    public void The_slow_gateway_is_invoked_exactly_once_across_repeated_identical_requests()
    {
        var slow = new SlowCarrierRateGateway();
        var proxy = new CachingRateGatewayProxy(slow);

        proxy.GetRate(Request);
        proxy.GetRate(Request);
        proxy.GetRate(Request);

        Assert.Equal(1, slow.Calls);
    }

    [Fact]
    public void A_different_request_misses_the_cache_and_calls_the_inner_gateway_again()
    {
        var slow = new SlowCarrierRateGateway();
        var proxy = new CachingRateGatewayProxy(slow);

        proxy.GetRate(Request);
        proxy.GetRate(new RateRequest("Rotterdam", "Lagos", 100m));

        Assert.Equal(2, slow.Calls);
    }

    [Fact]
    public void The_cached_value_matches_what_the_inner_gateway_would_have_returned_directly()
    {
        var slow = new SlowCarrierRateGateway();
        var proxy = new CachingRateGatewayProxy(slow);

        var direct = slow.GetRate(Request);
        var viaProxy = proxy.GetRate(Request);

        Assert.Equal(direct, viaProxy);
    }
}
