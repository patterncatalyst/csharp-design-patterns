namespace RateMath;

/// <summary>A palletized freight shipment, rated by weight and pallet count.</summary>
/// <param name="WeightKilograms">The pallet's combined weight, in kilograms.</param>
/// <param name="PalletCount">The number of pallets in the shipment.</param>
public sealed record FreightPallet(decimal WeightKilograms, int PalletCount) : IRateable<FreightPallet>
{
    private const decimal PerKilogramSurcharge = 0.20m;
    private const decimal PerPalletHandlingFee = 12.00m;

    public static Money BaseRate => new(35.00m);

    public static Money RateOf(FreightPallet shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        var weightCharge = new Money(shipment.WeightKilograms * PerKilogramSurcharge);
        var handlingCharge = new Money(shipment.PalletCount * PerPalletHandlingFee);
        return BaseRate + weightCharge + handlingCharge;
    }
}
