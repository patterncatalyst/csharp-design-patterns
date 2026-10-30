namespace BookingRequests;

/// <summary>
/// A monetary amount declared on a booking request or priced on one of its
/// line items. The primary constructor validates on every construction path,
/// including a bare <c>new Money(...)</c>, so an instance with a negative
/// amount or a malformed currency code can never exist. <see cref="Of"/> is
/// the call-site sugar every example in this file uses.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public decimal Amount { get; } = Amount >= 0m
        ? Amount
        : throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "A declared value cannot be negative.");

    public string Currency { get; } = Currency is { Length: 3 }
        ? Currency.ToUpperInvariant()
        : throw new ArgumentException("A currency code is a 3-letter ISO 4217 code, for example USD.", nameof(Currency));

    /// <summary>
    /// Creates a validated <see cref="Money"/> value, defaulting to US
    /// dollars since every booking in this example prices in the same
    /// currency.
    /// </summary>
    public static Money Of(decimal amount, string currency = "USD") => new(amount, currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
