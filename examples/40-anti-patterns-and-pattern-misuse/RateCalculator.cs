namespace FreightRating;

/// <summary>
/// The right-sized form: a freight rate quote is a handful of arithmetic
/// steps over one request, so it stays a single static method. No interface,
/// no factory, no injected strategy; there is only one way to rate a
/// consignment today, and this method says so directly.
/// </summary>
public static class RateCalculator
{
    public static decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseRate = request.DeclaredValue * 0.015m;
        var weight = request.WeightKilograms * 2.50m;
        var levy = request.FuelLevyApplies ? (baseRate + weight) * 0.08m : 0m;

        return baseRate + weight + levy;
    }
}
