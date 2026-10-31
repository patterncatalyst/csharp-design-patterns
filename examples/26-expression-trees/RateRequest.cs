namespace TariffExpressions;

/// <summary>
/// The shipment facts a tariff rule needs. Every rule in
/// <see cref="TariffExpressionLibrary"/> closes over exactly one
/// <see cref="RateRequest"/> parameter.
/// </summary>
public sealed record RateRequest(decimal WeightKilograms, decimal DeclaredValue, string Destination, bool Hazardous);
