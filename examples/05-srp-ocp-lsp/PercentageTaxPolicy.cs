namespace RateCalculation;

/// <summary>Applies a flat percentage tax on top of a subtotal.</summary>
public sealed class PercentageTaxPolicy(decimal taxRate) : ITaxPolicy
{
    private readonly decimal _taxRate = taxRate >= 0
        ? taxRate
        : throw new ArgumentOutOfRangeException(nameof(taxRate), taxRate, "Tax rate cannot be negative.");

    public Money ApplyTax(Money subtotal) => subtotal + subtotal.Scale(_taxRate);
}
