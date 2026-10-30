using Xunit;

namespace CarrierProvisioning.Tests;

/// <summary>
/// Proves the Factory Method and delegate-alternative shapes reach the same
/// result for the same carrier: <see cref="FastlineDispatcher"/> overrides
/// <see cref="CarrierDispatcher.CreateGateway"/> in a subclass, while
/// <see cref="DelegateCarrierDispatcher"/> gets the same gateway through a
/// constructor-supplied <see cref="Func{TResult}"/> instead.
/// </summary>
public class CarrierDispatcherTests
{
    private static readonly ShipmentRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void FastlineDispatcher_dispatches_through_the_overridden_gateway()
    {
        var dispatcher = new FastlineDispatcher();

        var confirmation = dispatcher.Dispatch(Request);

        Assert.Equal(new ShipmentConfirmation("FASTLINE", "FASTLINE-0020"), confirmation);
    }

    [Fact]
    public void DelegateCarrierDispatcher_reaches_parity_with_the_fastline_subclass()
    {
        var subclassDispatcher = new FastlineDispatcher();
        var delegateDispatcher = new DelegateCarrierDispatcher(
            () => new FastlineGateway(),
            new FastlineRequestBuilder(),
            new FastlineResponseParser());

        var subclassConfirmation = subclassDispatcher.Dispatch(Request);
        var delegateConfirmation = delegateDispatcher.Dispatch(Request);

        Assert.Equal(subclassConfirmation, delegateConfirmation);
    }
}
