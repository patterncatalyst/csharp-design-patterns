namespace FreightValueTypes;

/// <summary>
/// A monetary amount in a three-letter currency code (ISO 4217 style, e.g.
/// "USD"). A <c>readonly record struct</c> gives value equality, a
/// consistent hash code, and a no-allocation copy on every assignment with
/// zero boilerplate: the compiler generates <see cref="IEquatable{T}"/>,
/// <c>GetHashCode</c>, <c>==</c>, and <c>!=</c> from the two members below.
/// </summary>
/// <remarks>
/// The primary constructor's parameters are shadowed by the explicit
/// properties declared in the body, so construction can validate:
/// <c>new Money(-1m, "USD")</c> throws. That guard does not reach
/// <c>default(Money)</c> or <c>new Money[3]</c> — every struct has an
/// implicit parameterless state in .NET that bypasses every constructor.
/// Here that default is <c>Amount == 0m</c> and <c>Currency == null</c>,
/// which is not a value this type's own members (such as
/// <see cref="ToString"/>) can safely format. Use <see cref="Zero"/> for a
/// benign starting value; never read a field off a bare <c>default(Money)</c>.
/// </remarks>
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
    /// A benign zero amount in US dollars. The type's own replacement for
    /// <c>default(Money)</c>, which skips the validating constructor above.
    /// </summary>
    public static Money Zero { get; } = new(0m, "USD");

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
