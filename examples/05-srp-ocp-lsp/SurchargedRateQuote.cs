namespace RateCalculation;

/// <summary>
/// Decorates a base <see cref="IRateQuote"/> with zero or more
/// <see cref="ISurcharge"/> layers. Adding a new surcharge means adding a new
/// <see cref="ISurcharge"/> implementation and passing it in here; no base
/// pricing type ever changes (OCP).
/// </summary>
public sealed class SurchargedRateQuote(IRateQuote basePricing, IReadOnlyList<ISurcharge> surcharges) : IRateQuote
{
    public Money Quote(Consignment consignment)
    {
        var subtotal = basePricing.Quote(consignment);
        return surcharges.Aggregate(subtotal, (running, surcharge) => running + surcharge.Apply(running, consignment));
    }
}
