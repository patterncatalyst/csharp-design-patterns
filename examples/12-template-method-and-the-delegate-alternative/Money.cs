namespace CustomsDocumentation;

/// <summary>
/// A duty amount in a destination country's currency. Kept minimal and built
/// fresh for this example rather than shared with an earlier chapter.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public override string ToString() => $"{Amount:0.00} {Currency}";
}
