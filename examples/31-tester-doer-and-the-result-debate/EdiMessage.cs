namespace DeclarationParsing;

/// <summary>
/// A parsed customs EDI message: the message type carried by its first
/// segment, and every segment in the order they appeared on the wire.
/// </summary>
public sealed record EdiMessage(string MessageType, IReadOnlyList<EdiSegment> Segments);
