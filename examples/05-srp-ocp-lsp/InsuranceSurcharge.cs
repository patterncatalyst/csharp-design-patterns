namespace RateCalculation;

/// <summary>A surcharge proportional to the consignment's declared value, covering insurance.</summary>
public sealed class InsuranceSurcharge(decimal rateOfDeclaredValue) : ISurcharge
{
    private readonly decimal _rateOfDeclaredValue = rateOfDeclaredValue >= 0
        ? rateOfDeclaredValue
        : throw new ArgumentOutOfRangeException(
            nameof(rateOfDeclaredValue), rateOfDeclaredValue, "Insurance surcharge rate cannot be negative.");

    public Money Apply(Money subtotal, Consignment consignment) => consignment.DeclaredValue.Scale(_rateOfDeclaredValue);
}
