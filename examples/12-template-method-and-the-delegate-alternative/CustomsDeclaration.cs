namespace CustomsDocumentation;

/// <summary>
/// The completed customs declaration for a shipment: the fields a filer must
/// supply, the computed duty, and any validation messages raised while
/// assembling it. Both the inheritance-based builder and the delegate-based
/// engine produce this same record, which is what makes their outputs
/// directly comparable.
/// </summary>
public sealed record CustomsDeclaration(
    string DestinationCountry,
    IReadOnlyList<string> RequiredFields,
    Money Duty,
    IReadOnlyList<string> ValidationMessages);
