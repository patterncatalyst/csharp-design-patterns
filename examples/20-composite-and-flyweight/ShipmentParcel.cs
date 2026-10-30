namespace ConsignmentRouting;

/// <summary>
/// A Composite leaf: a single parcel bound for one <see cref="PortCode"/>.
/// <see cref="Destination"/> is the shared Flyweight instance for that port,
/// not a private copy, so every parcel routed through the same port points
/// at the one <see cref="PortCode"/> object the registry interned for it.
/// </summary>
public sealed record ShipmentParcel(string Reference, decimal WeightKilograms, PortCode Destination)
    : IConsignmentNode
{
    public decimal TotalWeightKilograms => WeightKilograms;

    public IEnumerable<ShipmentParcel> Parcels()
    {
        yield return this;
    }
}
