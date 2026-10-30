using Xunit;

namespace CarrierProvisioning.Tests;

/// <summary>
/// End-to-end: <see cref="ShipmentDispatchService"/> resolves a carrier's
/// whole product family from a <see cref="CarrierRegistry"/> by
/// <c>typeof(TCarrier)</c> and runs build, send, parse through it.
/// </summary>
public class ShipmentDispatchServiceTests
{
    private static readonly ShipmentRequest Request = new("Rotterdam", "Santos", 100m);

    [Fact]
    public void Dispatches_a_fastline_shipment_end_to_end()
    {
        var service = BuildService();

        var confirmation = service.Dispatch<Fastline>(Request);

        Assert.Equal(new ShipmentConfirmation("FASTLINE", "FASTLINE-0020"), confirmation);
    }

    [Fact]
    public void Dispatches_a_seabright_shipment_end_to_end()
    {
        var service = BuildService();

        var confirmation = service.Dispatch<Seabright>(Request);

        Assert.Equal(new ShipmentConfirmation("SEABRIGHT", "SEABRIGHT-0020"), confirmation);
    }

    [Fact]
    public void Dispatches_an_airco_shipment_end_to_end()
    {
        var service = BuildService();

        var confirmation = service.Dispatch<Airco>(Request);

        Assert.Equal(new ShipmentConfirmation("AIRCO", "AIRCO-0020"), confirmation);
    }

    private static ShipmentDispatchService BuildService()
    {
        var registry = new CarrierRegistry();
        registry.Register<Fastline>(new FastlineGatewayFactory());
        registry.Register<Seabright>(new SeabrightGatewayFactory());
        registry.Register<Airco>(new AircoGatewayFactory());

        return new ShipmentDispatchService(registry);
    }
}
