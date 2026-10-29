namespace ManifestWriting;

/// <summary>
/// One line of a consignment manifest: the shipment it describes, where it is
/// going, and how much it weighs. Written by <see cref="ManifestWriter"/>.
/// </summary>
public readonly record struct ManifestEntry(string ShipmentId, string Destination, decimal WeightKilograms);
