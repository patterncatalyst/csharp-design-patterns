namespace RateCalculation;

/// <summary>
/// An amount denominated in a specific ISO 4217 currency. A rate quote never
/// silently converts currencies: every collaborator that touches a
/// <see cref="Money"/> value either preserves its currency or throws.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public decimal Amount { get; init; } = Amount >= 0
        ? Amount
        : throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "Money amount cannot be negative.");

    public string Currency { get; init; } = IsIsoCurrencyCode(Currency)
        ? Currency
        : throw new ArgumentException("Currency must be a 3-letter uppercase ISO 4217 code.", nameof(Currency));

    /// <summary>Adds two amounts in the same currency. Never converts currencies.</summary>
    public static Money operator +(Money left, Money right) => left.Currency == right.Currency
        ? left with { Amount = left.Amount + right.Amount }
        : throw new InvalidOperationException(
            $"Cannot combine {left.Currency} and {right.Currency} without a currency conversion.");

    /// <summary>Scales the amount by a non-negative factor, keeping the currency.</summary>
    public Money Scale(decimal factor) => factor >= 0
        ? this with { Amount = Amount * factor }
        : throw new ArgumentOutOfRangeException(nameof(factor), factor, "Scale factor cannot be negative.");

    private static bool IsIsoCurrencyCode(string code) =>
        code.Length == 3 && code.All(char.IsAsciiLetterUpper);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
