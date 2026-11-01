namespace RateEngineCatalogues;

/// <summary>
/// A completed rate quote for one consignment, stamped with the instant the
/// quote was produced so a caller can tell a stale quote from a fresh one.
/// </summary>
public sealed record RateQuote(string ConsignmentId, decimal Amount, DateTimeOffset QuotedAt);
