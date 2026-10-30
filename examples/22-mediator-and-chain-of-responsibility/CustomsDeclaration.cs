namespace CustomsClearance;

/// <summary>
/// One consignment's declaration as it enters customs clearance: the facts a
/// <see cref="ClearanceStage"/> chain inspects, in the order the chain cares
/// about them, but never mutates.
/// </summary>
public sealed record CustomsDeclaration(
    string ConsignmentId,
    decimal DeclaredValue,
    bool DocumentsComplete,
    bool RequiresInspection,
    bool InspectionPassed);
