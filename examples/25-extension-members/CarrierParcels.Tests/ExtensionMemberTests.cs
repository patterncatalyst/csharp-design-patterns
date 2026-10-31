using CarrierParcels.Vendor;
using Xunit;

namespace CarrierParcels.Tests;

public class ExtensionMemberTests
{
    [Fact]
    public void WeightKilograms_converts_grams_to_kilograms()
    {
        var shipment = new CarrierShipment(2_500, "LHR", 10_000);

        Assert.Equal(2.5m, shipment.WeightKilograms);
    }

    public static TheoryData<int, bool> HeavyThresholdCases => new()
    {
        { 29_999, false },
        { 30_000, false },
        { 30_001, true },
    };

    [Theory]
    [MemberData(nameof(HeavyThresholdCases))]
    public void IsHeavy_is_true_above_the_threshold_and_false_below(int weightGrams, bool expected)
    {
        var shipment = new CarrierShipment(weightGrams, "JFK", 0);

        Assert.Equal(expected, shipment.IsHeavy);
    }

    [Fact]
    public void WithAddedWeight_returns_an_adapted_shipment_and_leaves_the_original_unchanged()
    {
        var original = new CarrierShipment(1_000, "ORD", 5_000);

        var adapted = original.WithAddedWeight(250);

        Assert.Equal(1_250, adapted.WeightGrams);
        Assert.Equal("ORD", adapted.DestinationCode);
        Assert.Equal(5_000, adapted.DeclaredValueMinorUnits);
        Assert.Equal(1_000, original.WeightGrams);
    }

    [Fact]
    public void Empty_static_member_has_zero_weight()
    {
        var empty = CarrierShipment.Empty;

        Assert.Equal(0, empty.WeightGrams);
        Assert.Equal(0, empty.DeclaredValueMinorUnits);
        Assert.Equal(string.Empty, empty.DestinationCode);
    }

    [Fact]
    public void FromKilograms_builds_a_shipment_with_the_expected_grams()
    {
        var shipment = CarrierShipment.FromKilograms(3.2m, "SIN");

        Assert.Equal(3_200, shipment.WeightGrams);
        Assert.Equal("SIN", shipment.DestinationCode);
    }

    [Fact]
    public void NormalisedDestination_classic_extension_method_upper_cases_and_trims()
    {
        var shipment = new CarrierShipment(100, "  lhr  ", 0);

        Assert.Equal("LHR", shipment.NormalisedDestination());
    }
}
