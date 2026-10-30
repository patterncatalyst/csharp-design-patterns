namespace CustomsDocumentation;

/// <summary>
/// A shipment awaiting a customs declaration: where it originates, where it
/// is bound, what it is declared to be worth and weigh, and the goods it
/// carries. Both declaration forms in this example read only from this
/// record; neither mutates it.
/// </summary>
public sealed record Shipment(
    string Origin,
    string DestinationCountry,
    decimal DeclaredValue,
    decimal WeightKilograms,
    IReadOnlyList<string> LineItems);
