namespace RatingEngine;

/// <summary>
/// A quoted monetary amount. Kept deliberately minimal: this example is about
/// the test suite, not the domain, so <see cref="Money"/> carries only what
/// <see cref="ShipmentRatingEngine"/> needs to compute and report a quote.
/// </summary>
public readonly record struct Money(decimal Amount)
{
    public override string ToString() => $"{Amount:0.00}";
}
