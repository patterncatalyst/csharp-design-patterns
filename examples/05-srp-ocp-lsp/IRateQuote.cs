namespace RateCalculation;

/// <summary>
/// The pricing contract every rate calculator honours: given a consignment,
/// produce a <see cref="Money"/> quote. Every implementation must return a
/// non-negative amount in the consignment's own currency, leave the
/// consignment untouched, and never charge less for a heavier consignment.
/// Both constraints are exercised as a Liskov substitution contract in
/// <c>LspInvariantTests</c>.
/// </summary>
public interface IRateQuote
{
    Money Quote(Consignment consignment);
}
