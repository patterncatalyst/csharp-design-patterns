namespace RateCalculation;

/// <summary>A surcharge proportional to the priced subtotal, tracking fuel cost.</summary>
public sealed class FuelSurcharge(decimal rateOfSubtotal) : ISurcharge
{
    private readonly decimal _rateOfSubtotal = rateOfSubtotal >= 0
        ? rateOfSubtotal
        : throw new ArgumentOutOfRangeException(
            nameof(rateOfSubtotal), rateOfSubtotal, "Fuel surcharge rate cannot be negative.");

    public Money Apply(Money subtotal, Consignment consignment) => subtotal.Scale(_rateOfSubtotal);
}
