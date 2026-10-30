using Xunit;

namespace CarrierProvisioning.Tests;

/// <summary>
/// Proves the static-abstract reification: a carrier's own
/// <see cref="ICarrier.Code"/> ends up on the <see cref="CarrierRequest"/>
/// its matching builder produces, read directly off the carrier type rather
/// than passed in by the caller.
/// </summary>
public class RequestBuilderTests
{
    private static readonly ShipmentRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void FastlineRequestBuilder_stamps_the_fastline_code()
    {
        var carrierRequest = new FastlineRequestBuilder().Build(Request);

        Assert.Equal(Fastline.Code, carrierRequest.Carrier);
    }

    [Fact]
    public void SeabrightRequestBuilder_stamps_the_seabright_code()
    {
        var carrierRequest = new SeabrightRequestBuilder().Build(Request);

        Assert.Equal(Seabright.Code, carrierRequest.Carrier);
    }

    [Fact]
    public void AircoRequestBuilder_stamps_the_airco_code()
    {
        var carrierRequest = new AircoRequestBuilder().Build(Request);

        Assert.Equal(Airco.Code, carrierRequest.Carrier);
    }
}
