using FreightRating.OverAbstracted;
using Xunit;

namespace FreightRating.Tests;

public class EquivalenceTests
{
    public static TheoryData<RateRequest> Requests => new()
    {
        new RateRequest(DeclaredValue: 0m, WeightKilograms: 0m, FuelLevyApplies: false),
        new RateRequest(DeclaredValue: 0m, WeightKilograms: 0m, FuelLevyApplies: true),
        new RateRequest(DeclaredValue: 1_000m, WeightKilograms: 100m, FuelLevyApplies: false),
        new RateRequest(DeclaredValue: 1_000m, WeightKilograms: 100m, FuelLevyApplies: true),
        new RateRequest(DeclaredValue: 42.75m, WeightKilograms: 3.5m, FuelLevyApplies: true),
        new RateRequest(DeclaredValue: 999_999.99m, WeightKilograms: 50_000m, FuelLevyApplies: true),
        new RateRequest(DeclaredValue: 999_999.99m, WeightKilograms: 50_000m, FuelLevyApplies: false),
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public void Both_forms_produce_the_same_quote(RateRequest request)
    {
        var collapsed = RateCalculator.Quote(request);
        var tower = new RateEngine(new StandardRateComponentFactory()).Quote(request);

        Assert.Equal(collapsed, tower);
    }
}
