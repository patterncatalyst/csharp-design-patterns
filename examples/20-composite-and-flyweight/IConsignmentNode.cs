namespace ConsignmentRouting;

/// <summary>
/// A node in a consignment tree: either a single <see cref="ShipmentParcel"/>
/// leaf or a <see cref="Consolidation"/> of other nodes. Both answer the same
/// two questions without the caller ever checking which one it is holding.
/// </summary>
public interface IConsignmentNode
{
    /// <summary>
    /// The weight of this node: the parcel's own weight for a leaf, or the
    /// recursive sum of every descendant leaf for a consolidation.
    /// </summary>
    decimal TotalWeightKilograms { get; }

    /// <summary>
    /// Every leaf parcel reachable from this node: itself for a leaf, or the
    /// flattened, recursive union of every child's parcels for a
    /// consolidation.
    /// </summary>
    IEnumerable<ShipmentParcel> Parcels();
}
