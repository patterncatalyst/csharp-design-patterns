namespace TariffRules;

/// <summary>
/// The context a <see cref="TariffRule"/> is evaluated against: the Interpreter
/// pattern's "context" argument, carried alongside the expression tree.
/// </summary>
public sealed record Shipment(decimal WeightKilograms, decimal DeclaredValue, string Destination);
