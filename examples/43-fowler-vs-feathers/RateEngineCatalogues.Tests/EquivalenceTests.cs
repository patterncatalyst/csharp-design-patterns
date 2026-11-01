using Xunit;

namespace RateEngineCatalogues.Tests;

/// <summary>
/// Proves the Feathers-stage <see cref="SeamedRateEngine"/> and the
/// Fowler-stage <see cref="RateEngine"/> compute the identical amount for the
/// same request, the same <see cref="StandardTariffSource"/>, and the same
/// fixed clock instant, across every <see cref="ServiceLevel"/> and both
/// inside and outside the peak window: the refactor changes the method's
/// shape, never its behavior.
/// </summary>
public class EquivalenceTests
{
    private static readonly DateTimeOffset OffPeak = new(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Peak = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<RateRequest, DateTimeOffset> RequestsAndClockInstants => new()
    {
        { new RateRequest("CN-5001", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, ServiceLevel.Standard), OffPeak },
        { new RateRequest("CN-5002", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, ServiceLevel.Standard), Peak },
        { new RateRequest("CN-5003", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, ServiceLevel.Express), OffPeak },
        { new RateRequest("CN-5004", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, ServiceLevel.Express), Peak },
        { new RateRequest("CN-5005", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, ServiceLevel.Overnight), OffPeak },
        { new RateRequest("CN-5006", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, ServiceLevel.Overnight), Peak },
        { new RateRequest("CN-5007", DeclaredValue: 0m, WeightKilograms: 0m, ServiceLevel.Standard), OffPeak },
        { new RateRequest("CN-5008", DeclaredValue: 42.75m, WeightKilograms: 3.5m, ServiceLevel.Overnight), Peak },
    };

    [Theory]
    [MemberData(nameof(RequestsAndClockInstants))]
    public void Both_catalogues_produce_the_same_quote(RateRequest request, DateTimeOffset at)
    {
        var tariffs = new StandardTariffSource();
        var clock = new FakeClock(at);
        var seamed = new SeamedRateEngine(tariffs, clock);
        var refactored = new RateEngine(tariffs, clock);
        var serviceLevel = request.Service switch
        {
            ServiceLevel.Express => "EXPRESS",
            ServiceLevel.Overnight => "OVERNIGHT",
            _ => "STANDARD",
        };

        var seamedAmount = seamed.Quote(request.DeclaredValue, request.WeightKilograms, serviceLevel);
        var refactoredAmount = refactored.Quote(request).Amount;

        Assert.Equal(seamedAmount, refactoredAmount);
    }
}
