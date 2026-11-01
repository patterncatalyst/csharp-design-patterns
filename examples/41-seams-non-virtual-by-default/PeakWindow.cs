namespace RateEngineSeams;

/// <summary>
/// The peak-hour rule shared by the refactored <see cref="RateEngine"/>'s
/// default surcharge: a consignment quoted between 08:00 and 18:00 (exclusive)
/// at the given instant falls inside the peak window.
/// </summary>
internal static class PeakWindow
{
    internal static bool IsPeak(DateTimeOffset at) => at.Hour is >= 8 and < 18;
}
