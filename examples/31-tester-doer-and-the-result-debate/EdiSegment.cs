namespace DeclarationParsing;

/// <summary>
/// One line of a customs EDI message: a tag (for example <c>UNH</c> or
/// <c>LIN</c>) followed by zero or more data elements.
/// </summary>
public sealed record EdiSegment(string Tag, IReadOnlyList<string> Elements);
