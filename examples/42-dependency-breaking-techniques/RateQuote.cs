namespace RateEngineDependencies;

/// <summary>
/// A finalized quote for a consignment: the computed amount, who quoted it,
/// and when. Returned by both the legacy and the refactored
/// <c>RateEngine</c>.
/// </summary>
public sealed record RateQuote(string Reference, string ConsignmentId, decimal Amount, string QuotedBy, DateTimeOffset QuotedAt);
