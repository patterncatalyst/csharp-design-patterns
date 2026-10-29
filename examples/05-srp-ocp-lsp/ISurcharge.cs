namespace RateCalculation;

/// <summary>
/// An additional charge layered on top of a priced subtotal. New surcharge
/// types (fuel, hazmat handling, peak season) are added by implementing this
/// interface, never by editing an <see cref="IRateQuote"/> implementation
/// (OCP).
/// </summary>
public interface ISurcharge
{
    Money Apply(Money subtotal, Consignment consignment);
}
