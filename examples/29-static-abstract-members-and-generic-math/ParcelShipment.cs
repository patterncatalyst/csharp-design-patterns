namespace RateMath;

/// <summary>A small parcel, rated by weight above a base handling fee.</summary>
/// <param name="WeightKilograms">The parcel's weight, in kilograms.</param>
public sealed record ParcelShipment(decimal WeightKilograms) : IRateable<ParcelShipment>
{
    private const decimal PerKilogramSurcharge = 0.75m;

    public static Money BaseRate => new(4.50m);

    public static Money RateOf(ParcelShipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return BaseRate + new Money(shipment.WeightKilograms * PerKilogramSurcharge);
    }
}
