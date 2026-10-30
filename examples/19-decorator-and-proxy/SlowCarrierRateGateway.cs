namespace RateComposition;

/// <summary>
/// Stands in for a carrier's rate lookup that is expensive to call, for
/// example a network round trip. <see cref="Calls"/> counts how many times
/// <see cref="GetRate"/> actually ran, making the cost of calling it
/// observable without any real network or clock dependency to fake out.
/// </summary>
public sealed class SlowCarrierRateGateway : ICarrierRateGateway
{
    public int Calls { get; private set; }

    public CarrierRate GetRate(RateRequest request)
    {
        Calls++;

        return new CarrierRate(Money.Of(request.Kilograms * 7.50m), TransitDays: 3);
    }
}
