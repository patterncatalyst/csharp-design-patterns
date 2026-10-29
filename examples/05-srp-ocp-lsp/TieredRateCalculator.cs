namespace RateCalculation;

/// <summary>
/// Prices a consignment with a bulk-discount tier: the first stretch of
/// kilograms at one rate, the remainder at a lower rate. A second,
/// independently substitutable <see cref="IRateQuote"/> implementation: it
/// honours the same invariants as <see cref="WeightBasedRateCalculator"/>
/// despite a completely different pricing formula (LSP).
/// </summary>
public sealed class TieredRateCalculator(
    decimal firstTierKilograms,
    decimal firstTierRatePerKilogram,
    decimal additionalRatePerKilogram) : IRateQuote
{
    private readonly decimal _firstTierKilograms = firstTierKilograms > 0
        ? firstTierKilograms
        : throw new ArgumentOutOfRangeException(
            nameof(firstTierKilograms), firstTierKilograms, "First tier size must be positive.");

    private readonly decimal _firstTierRatePerKilogram = firstTierRatePerKilogram > 0
        ? firstTierRatePerKilogram
        : throw new ArgumentOutOfRangeException(
            nameof(firstTierRatePerKilogram), firstTierRatePerKilogram, "First tier rate must be positive.");

    private readonly decimal _additionalRatePerKilogram = additionalRatePerKilogram > 0
        ? additionalRatePerKilogram
        : throw new ArgumentOutOfRangeException(
            nameof(additionalRatePerKilogram), additionalRatePerKilogram, "Additional rate must be positive.");

    public Money Quote(Consignment consignment)
    {
        var weight = consignment.Weight.Kilograms;
        var billableAtFirstTier = Math.Min(weight, _firstTierKilograms);
        var billableAtAdditionalRate = Math.Max(weight - _firstTierKilograms, 0m);
        var amount = (billableAtFirstTier * _firstTierRatePerKilogram)
            + (billableAtAdditionalRate * _additionalRatePerKilogram);
        return new Money(amount, consignment.DeclaredValue.Currency);
    }
}
