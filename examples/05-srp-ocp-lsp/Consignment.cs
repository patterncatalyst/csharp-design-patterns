namespace RateCalculation;

/// <summary>
/// A shipment awaiting a rate quote: its weight and its declared value for
/// insurance and customs purposes. Immutable, so no collaborator in this
/// example can mutate a consignment while quoting it.
/// </summary>
public sealed record Consignment(Weight Weight, Money DeclaredValue);
