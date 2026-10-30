namespace RateComposition;

/// <summary>
/// A monetary amount produced by rating a shipment. The primary constructor
/// validates on every construction path, including a bare
/// <c>new Money(...)</c>, so an instance with a negative amount can never
/// exist.
/// </summary>
public readonly record struct Money(decimal Amount)
{
    public decimal Amount { get; } = Amount >= 0m
        ? Amount
        : throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "A rated amount cannot be negative.");

    /// <summary>
    /// Creates a validated <see cref="Money"/> value. The call-site sugar
    /// every calculator, decorator, and proxy in this example uses instead
    /// of the positional constructor directly.
    /// </summary>
    public static Money Of(decimal amount) => new(amount);

    public override string ToString() => $"{Amount:0.00}";
}
