namespace DocumentDispatch;

/// <summary>
/// The rendered output for a <see cref="DocumentRequest"/>: the same kind and
/// consignment, plus the document body a renderer built from its fields.
/// </summary>
public sealed record RenderedDocument(DocumentKind Kind, string ConsignmentId, string Body);
