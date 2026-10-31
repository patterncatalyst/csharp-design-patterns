using CarrierParcels.Vendor;
using Xunit;

namespace CarrierParcels.Tests;

public class ExtensionOperatorTests
{
    [Fact]
    public void Operator_plus_combines_two_shipments_weights_and_values()
    {
        var left = new CarrierShipment(1_000, "ORD", 5_000);
        var right = new CarrierShipment(500, "DFW", 2_000);

        var combined = left + right;

        Assert.Equal(1_500, combined.WeightGrams);
        Assert.Equal("ORD", combined.DestinationCode);
        Assert.Equal(7_000, combined.DeclaredValueMinorUnits);
    }

    [Fact]
    public void Extension_members_resolve_on_the_unowned_sealed_type()
    {
        // A static extension member (FromKilograms) builds the sealed vendor
        // type, an instance extension property (WeightKilograms) reads it back,
        // and the extension operator (+) combines it with another instance,
        // all without CarrierShipment itself declaring any of the three.
        var fromKilograms = CarrierShipment.FromKilograms(1.5m, "AMS");
        var combined = fromKilograms + CarrierShipment.Empty;

        Assert.Equal(1_500, fromKilograms.WeightGrams);
        Assert.Equal(1.5m, fromKilograms.WeightKilograms);
        Assert.Equal(1_500, combined.WeightGrams);
    }
}
