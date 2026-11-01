using Xunit;

namespace RateEngineReplacement.Tests;

/// <summary>
/// Proves the refactor-in-place and the clean-rewrite replacements satisfy
/// the exact same contract: for identical input, the same
/// <see cref="StandardTariffSource"/>, and the same fixed clock,
/// <see cref="RefactoredRateEngine"/> and <see cref="RewrittenRateEngine"/>
/// produce the identical <see cref="RateQuote"/>, checked through record
/// value equality rather than field by field, across a scenario set spanning
/// both sides of the peak window and more than one lane.
/// </summary>
public class EquivalenceTests
{
    private static readonly DateTimeOffset OffPeak = new(2026, 11, 1, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Peak = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<RateRequest, DateTimeOffset> Scenarios => new()
    {
        { new RateRequest("CN-5000", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, LaneCode: "STD"), OffPeak },
        { new RateRequest("CN-5001", DeclaredValue: 1000.00m, WeightKilograms: 200.00m, LaneCode: "STD"), Peak },
        { new RateRequest("CN-5002", DeclaredValue: 2500.50m, WeightKilograms: 75.25m, LaneCode: "EXP"), OffPeak },
        { new RateRequest("CN-5003", DeclaredValue: 2500.50m, WeightKilograms: 75.25m, LaneCode: "EXP"), Peak },
        { new RateRequest("CN-5004", DeclaredValue: 0m, WeightKilograms: 0m, LaneCode: "INTL"), OffPeak },
        { new RateRequest("CN-5005", DeclaredValue: 999_999.99m, WeightKilograms: 50_000m, LaneCode: "INTL"), Peak },
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Both_implementations_satisfy_the_same_contract(RateRequest request, DateTimeOffset at)
    {
        var tariffs = new StandardTariffSource();
        var clock = new FakeClock(at);

        var refactored = new RefactoredRateEngine(tariffs, clock).Quote(request);
        var rewritten = new RewrittenRateEngine(tariffs, clock).Quote(request);

        Assert.Equal(refactored, rewritten);
    }
}
