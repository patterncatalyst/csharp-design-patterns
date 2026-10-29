namespace RatingEngine;

/// <summary>
/// A shipment awaiting a rate quote: the identifier it is tracked under, its
/// destination, and its weight in kilograms.
/// </summary>
public sealed record Consignment(string ConsignmentId, string Destination, decimal Kilograms);
