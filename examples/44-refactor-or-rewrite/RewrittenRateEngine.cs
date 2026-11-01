namespace RateEngineReplacement;

/// <summary>
/// The clean-rewrite replacement: the same contract as
/// <see cref="RefactoredRateEngine"/>, computed as one consolidated
/// expression instead of a running subtotal built up statement by statement.
/// Both engines are exercised against the same inputs and the same fixed
/// clock to prove a rewrite is not a silent behavior change, the whole point
/// of choosing "rewrite" over "refactor" deliberately rather than by default.
/// </summary>
public sealed class RewrittenRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine
{
    /// <summary>
    /// Quotes a rate as a single expression: the ad-valorem and per-kilogram
    /// components combined into one base amount, then the peak surcharge
    /// applied as a multiplier over that amount rather than as a separate
    /// mutating step.
    /// </summary>
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var at = clock.UtcNow;
        var baseAmount = (request.DeclaredValue * RateRules.DeclaredValueRate)
            + (request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode));
        var total = PeakWindow.IsPeak(at) ? baseAmount * (1m + RateRules.PeakMultiplier) : baseAmount;

        return new RateQuote(request.ConsignmentId, total, at);
    }
}
