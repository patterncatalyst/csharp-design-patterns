namespace RateCalculation;

/// <summary>
/// Prices a consignment at a flat rate per kilogram. Pricing is its only
/// responsibility: it has no tax or audit collaborator to misuse (SRP).
/// </summary>
public sealed class WeightBasedRateCalculator(decimal ratePerKilogram) : IRateQuote
{
    private readonly decimal _ratePerKilogram = ratePerKilogram > 0
        ? ratePerKilogram
        : throw new ArgumentOutOfRangeException(
            nameof(ratePerKilogram), ratePerKilogram, "Rate per kilogram must be positive.");

    public Money Quote(Consignment consignment) =>
        new(consignment.Weight.Kilograms * _ratePerKilogram, consignment.DeclaredValue.Currency);
}
