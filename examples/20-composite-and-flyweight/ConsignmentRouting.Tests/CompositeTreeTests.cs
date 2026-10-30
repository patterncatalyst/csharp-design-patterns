using Xunit;

namespace ConsignmentRouting.Tests;

/// <summary>
/// Proves the Composite shape: a <see cref="Consolidation"/> nested inside
/// another <see cref="Consolidation"/> answers <see cref="IConsignmentNode.TotalWeightKilograms"/>
/// and <see cref="IConsignmentNode.Parcels"/> the same way a single
/// <see cref="ShipmentParcel"/> leaf does, without the tree's depth ever
/// showing up at the call site.
/// </summary>
public class CompositeTreeTests
{
    private static readonly PortCodeRegistry Ports = new();

    [Fact]
    public void A_nested_consolidation_sums_the_weight_of_every_leaf_parcel()
    {
        var rotterdam = Ports.Get("NLRTM");
        var hamburg = Ports.Get("DEHAM");

        var innerConsolidation = new Consolidation("CONSOL-INNER", [
            new ShipmentParcel("PARCEL-1", 12.5m, rotterdam),
            new ShipmentParcel("PARCEL-2", 7.25m, rotterdam),
        ]);
        var outerConsolidation = new Consolidation("CONSOL-OUTER", [
            innerConsolidation,
            new ShipmentParcel("PARCEL-3", 40.0m, hamburg),
        ]);

        Assert.Equal(59.75m, outerConsolidation.TotalWeightKilograms);
    }

    [Fact]
    public void Parcels_traverses_every_leaf_of_a_nested_tree()
    {
        var singapore = Ports.Get("SGSIN");

        var innerConsolidation = new Consolidation("CONSOL-INNER", [
            new ShipmentParcel("PARCEL-A", 3.0m, singapore),
            new ShipmentParcel("PARCEL-B", 4.0m, singapore),
        ]);
        var middleConsolidation = new Consolidation("CONSOL-MIDDLE", [
            innerConsolidation,
            new ShipmentParcel("PARCEL-C", 5.0m, singapore),
        ]);
        var outerConsolidation = new Consolidation("CONSOL-OUTER", [
            middleConsolidation,
            new ShipmentParcel("PARCEL-D", 6.0m, singapore),
        ]);

        var references = outerConsolidation.Parcels().Select(parcel => parcel.Reference).ToList();

        Assert.Equal(4, references.Count);
        Assert.Equal(
            ["PARCEL-A", "PARCEL-B", "PARCEL-C", "PARCEL-D"],
            references.OrderBy(reference => reference, StringComparer.Ordinal));
    }

    [Fact]
    public void A_leaf_parcel_reports_its_own_weight_and_yields_only_itself()
    {
        var losAngeles = Ports.Get("USLAX");
        var parcel = new ShipmentParcel("PARCEL-SOLO", 18.0m, losAngeles);

        Assert.Equal(18.0m, parcel.TotalWeightKilograms);
        Assert.Equal([parcel], parcel.Parcels());
    }
}
