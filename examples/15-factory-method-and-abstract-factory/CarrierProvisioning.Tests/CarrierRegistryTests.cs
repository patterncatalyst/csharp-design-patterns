using Xunit;

namespace CarrierProvisioning.Tests;

/// <summary>
/// Proves the reified-generics headline: <see cref="CarrierRegistry"/>
/// resolves a carrier's whole product family by <c>typeof(TCarrier)</c>,
/// the Fastline/Seabright/Airco families never collide, a new carrier is
/// added with one <see cref="CarrierRegistry.Register{TCarrier}"/> call and
/// no edit to the registry, and an unresolved carrier fails loudly.
/// </summary>
public class CarrierRegistryTests
{
    [Fact]
    public void Resolves_the_fastline_family_by_type()
    {
        var registry = BuildRegistryWithoutAirco();

        var factory = registry.FactoryFor<Fastline>();

        Assert.IsType<FastlineGateway>(factory.CreateGateway());
        Assert.IsType<FastlineRequestBuilder>(factory.CreateRequestBuilder());
        Assert.IsType<FastlineResponseParser>(factory.CreateParser());
    }

    [Theory]
    [InlineData("fastline")]
    [InlineData("seabright")]
    [InlineData("airco")]
    public void Each_carrier_key_resolves_its_own_family(string carrier)
    {
        var registry = BuildFullRegistry();

        switch (carrier)
        {
            case "fastline":
                AssertFamily<Fastline>(registry, "Fastline");
                break;
            case "seabright":
                AssertFamily<Seabright>(registry, "Seabright");
                break;
            default:
                AssertFamily<Airco>(registry, "Airco");
                break;
        }
    }

    [Fact]
    public void A_new_carrier_resolves_after_one_register_call_with_no_registry_edit()
    {
        var registry = BuildRegistryWithoutAirco();

        registry.Register<Airco>(new AircoGatewayFactory());

        var factory = registry.FactoryFor<Airco>();
        Assert.IsType<AircoGateway>(factory.CreateGateway());
    }

    [Fact]
    public void An_unregistered_carrier_throws_unknown_carrier_exception()
    {
        var registry = BuildRegistryWithoutAirco();

        Assert.Throws<UnknownCarrierException>(() => registry.FactoryFor<Airco>());
    }

    private static void AssertFamily<TCarrier>(CarrierRegistry registry, string expectedPrefix)
        where TCarrier : ICarrier
    {
        var factory = registry.FactoryFor<TCarrier>();

        Assert.StartsWith(expectedPrefix, factory.CreateGateway().GetType().Name);
        Assert.StartsWith(expectedPrefix, factory.CreateRequestBuilder().GetType().Name);
        Assert.StartsWith(expectedPrefix, factory.CreateParser().GetType().Name);
    }

    private static CarrierRegistry BuildRegistryWithoutAirco()
    {
        var registry = new CarrierRegistry();
        registry.Register<Fastline>(new FastlineGatewayFactory());
        registry.Register<Seabright>(new SeabrightGatewayFactory());
        return registry;
    }

    private static CarrierRegistry BuildFullRegistry()
    {
        var registry = BuildRegistryWithoutAirco();
        registry.Register<Airco>(new AircoGatewayFactory());
        return registry;
    }
}
