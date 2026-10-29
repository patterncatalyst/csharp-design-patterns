namespace ConsignmentAmendments;

/// <summary>
/// A declared value in a three-letter currency code (ISO 4217 style, e.g.
/// "EUR"). A <c>readonly record struct</c> gives value equality, a stable
/// hash code, and a no-allocation copy on every assignment from the
/// declaration alone.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    /// <summary>The amount, validated non-negative at construction.</summary>
    public decimal Amount { get; init; } = Amount >= 0m
        ? Amount
        : throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "Money amount cannot be negative.");

    /// <summary>The three-letter currency code, validated at construction.</summary>
    public string Currency { get; init; } = Currency is { Length: 3 }
        ? Currency
        : throw new ArgumentException("Currency must be a 3-letter code.", nameof(Currency));

    /// <summary>
    /// A benign zero declared value in euros. The type's own replacement for
    /// <c>default(Money)</c>, which skips the validating constructor above
    /// and leaves <c>Currency</c> <see langword="null"/>.
    /// </summary>
    public static Money Zero { get; } = new(0m, "EUR");

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
