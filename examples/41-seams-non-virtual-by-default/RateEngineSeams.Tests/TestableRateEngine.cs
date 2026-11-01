namespace RateEngineSeams.Tests;

/// <summary>
/// The extract-and-override seam in action: a test subclass that replaces
/// the peak-hour surcharge with a fixed value, independent of
/// <see cref="PeakWindow"/> or the clock, something the sealed legacy
/// <c>RateEngine</c> has no way to permit.
/// </summary>
public sealed class TestableRateEngine(ITariffSource tariffs, IClock clock) : RateEngine(tariffs, clock)
{
    public const decimal FixedSurcharge = 7.00m;

    protected override decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) => FixedSurcharge;
}
