namespace DeclarationParsing;

/// <summary>
/// A customs declaration before it has been checked for processing. Both the
/// Tester-Doer side (<see cref="DeclarationProcessor"/>) and the
/// <see cref="Result{T}"/> side (<see cref="DeclarationValidator"/>) validate
/// the same four fields, each in its own idiom.
/// </summary>
public sealed record CustomsDeclaration(
    string Reference,
    string CommodityCode,
    decimal DeclaredValue,
    string OriginCountry);
