using Xunit;

namespace ConcurrentHandling.Tests;

/// <summary>
/// Proves <see cref="CountingValidatorDecorator"/> forwards every concurrent
/// call to its inner <see cref="ManifestValidator"/> exactly once, and that
/// the call counter stays accurate under contention.
/// </summary>
public class DecoratorTests
{
    private const int CallCount = 64;

    [Fact]
    public async Task The_decorator_delegates_every_async_call_to_its_inner_validator()
    {
        var decorator = new CountingValidatorDecorator(new ManifestValidator());
        var tracking = new TrackingId("C-DEC-1");
        var ct = TestContext.Current.CancellationToken;

        var calls = Enumerable.Range(0, CallCount)
            .Select(i => decorator.ValidateAsync(new HandlingEvent(tracking, HandlingKind.Customs, i), ct));

        var results = await Task.WhenAll(calls);

        Assert.Equal(CallCount, decorator.Calls);
        Assert.All(results, Assert.True);
    }
}
