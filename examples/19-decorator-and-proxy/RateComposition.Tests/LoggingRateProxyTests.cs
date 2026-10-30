using Xunit;

namespace RateComposition.Tests;

/// <summary>
/// Proves the <c>DispatchProxy</c> shape: <see cref="RateProxyFactory.Wrap"/>
/// returns an <see cref="IRateCalculator"/> that forwards calls to the real
/// target and produces the same result, while
/// <see cref="LoggingRateProxy.InvokedMembers"/> records which member was
/// actually invoked, proving interception happened rather than the caller
/// having been handed the target directly.
/// </summary>
public class LoggingRateProxyTests
{
    private static readonly RateRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void The_proxy_forwards_Quote_to_the_target_and_returns_the_same_result()
    {
        var target = new BaseRateCalculator(5.00m);

        var proxy = RateProxyFactory.Wrap(target, out var invokedMembers);
        var result = proxy.Quote(Request);

        Assert.Equal(target.Quote(Request), result);
        Assert.Contains(nameof(IRateCalculator.Quote), invokedMembers);
    }

    [Fact]
    public void The_proxy_records_one_invocation_per_call_made_through_it()
    {
        var target = new BaseRateCalculator(5.00m);
        var proxy = RateProxyFactory.Wrap(target, out var invokedMembers);

        proxy.Quote(Request);
        proxy.Quote(Request);

        Assert.Equal(2, invokedMembers.Count);
        Assert.All(invokedMembers, member => Assert.Equal(nameof(IRateCalculator.Quote), member));
    }

    [Fact]
    public void The_proxy_forwards_to_a_decorated_target_just_as_faithfully_as_a_bare_one()
    {
        var decorated = new TaxRateDecorator(new BaseRateCalculator(5.00m), rate: 0.08m);

        var proxy = RateProxyFactory.Wrap(decorated, out var invokedMembers);
        var result = proxy.Quote(Request);

        Assert.Equal(decorated.Quote(Request), result);
        Assert.Single(invokedMembers);
    }
}
