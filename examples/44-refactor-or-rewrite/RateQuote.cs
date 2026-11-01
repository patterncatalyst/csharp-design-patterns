namespace RateEngineReplacement;

/// <summary>
/// A completed rate quote for one consignment, stamped with the instant the
/// quote was produced. Both replacement engines return the same
/// <see cref="RateQuote"/> shape for identical input, which is what the
/// equivalence test asserts through record value equality.
/// </summary>
public sealed record RateQuote(string ConsignmentId, decimal Amount, DateTimeOffset QuotedAt);
