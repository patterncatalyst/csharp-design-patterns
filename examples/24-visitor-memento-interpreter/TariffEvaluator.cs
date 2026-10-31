namespace TariffRules;

/// <summary>
/// A pattern-matching alternative to a classic Visitor: instead of a
/// <c>Visit</c> method dispatched per concrete <see cref="TariffRule"/>, a
/// single recursive switch expression performs the same double dispatch that
/// the Visitor pattern exists to provide, using the language's own exhaustive
/// pattern matching over the closed hierarchy.
/// </summary>
public static class TariffEvaluator
{
    public static decimal Evaluate(TariffRule rule, Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(shipment);

        return rule switch
        {
            FlatFee f => f.Amount,
            PerKilogram p => p.Rate * shipment.WeightKilograms,
            Surcharge s => Evaluate(s.Inner, shipment) * (1 + s.Percent / 100m),
            Sum s => s.Rules.Sum(r => Evaluate(r, shipment)),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
    }
}
