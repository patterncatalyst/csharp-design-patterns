namespace RateEngineSeams;

/// <summary>
/// The refactored, seam-enabled rate engine: both collaborators the legacy
/// shape hard-wired, the tariff table and the clock, arrive through the
/// constructor instead, and the peak-hour surcharge itself is an extract-and-
/// override seam a subclass can replace. Not sealed, so a test subclass can
/// override <see cref="PeakSurcharge"/> directly, the other seam this example
/// shows alongside constructor injection.
/// </summary>
public class RateEngine(ITariffSource tariffs, IClock clock)
{
    /// <summary>
    /// Quotes a rate using the injected <see cref="ITariffSource"/> for the
    /// per-kilogram rate and the injected <see cref="IClock"/> for the instant
    /// the peak-hour surcharge is evaluated against and the quote is stamped
    /// with.
    /// </summary>
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = request.DeclaredValue * 0.015m;
        subtotal += request.WeightKilograms * tariffs.RatePerKilogram("STD");

        var at = clock.UtcNow;
        var total = subtotal + PeakSurcharge(subtotal, at);

        return new RateQuote(request.ConsignmentId, total, at);
    }

    /// <summary>
    /// The extract-and-override seam: a subclass can replace the peak-hour
    /// surcharge rule entirely without touching <see cref="Quote"/>, in
    /// addition to the constructor seam over the tariff source and the clock.
    /// </summary>
    protected virtual decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) =>
        PeakWindow.IsPeak(at) ? subtotal * 0.1m : 0m;
}
