namespace DocumentDispatch;

/// <summary>
/// A request to render one shipping document for a consignment, carrying the
/// field values the chosen <see cref="DocumentKind"/>'s renderer needs.
/// </summary>
public sealed record DocumentRequest(DocumentKind Kind, string ConsignmentId, IReadOnlyDictionary<string, string> Fields);
