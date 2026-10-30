namespace ConsignmentRouting;

/// <summary>
/// A Composite branch: a named grouping of child nodes, each of which may
/// itself be a <see cref="ShipmentParcel"/> leaf or another
/// <see cref="Consolidation"/>. A consignment tree of arbitrary depth answers
/// <see cref="TotalWeightKilograms"/> and <see cref="Parcels"/> the same way
/// at every level, so a carrier routing routine never needs to know how deep
/// the tree it was handed actually is.
/// </summary>
public sealed class Consolidation(string Reference, IReadOnlyList<IConsignmentNode> Children) : IConsignmentNode
{
    public string Reference { get; } = Reference;

    public IReadOnlyList<IConsignmentNode> Children { get; } = Children;

    public decimal TotalWeightKilograms => Children.Sum(child => child.TotalWeightKilograms);

    public IEnumerable<ShipmentParcel> Parcels() => Children.SelectMany(child => child.Parcels());
}
