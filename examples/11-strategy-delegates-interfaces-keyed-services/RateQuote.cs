namespace RateQuoting;

/// <summary>
/// A priced answer to a <see cref="RateRequest"/>, returned by all three
/// quoting tiers in this example regardless of how the pricing logic behind
/// it was selected.
/// </summary>
public sealed record RateQuote(string Destination, Money Price, string ServiceLevel);
