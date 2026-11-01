namespace RateEngineReplacement;

/// <summary>
/// The refactor-in-place replacement: the legacy calculation moved behind
/// <see cref="IRateEngine"/> one step at a time, the tariff table and the
/// clock replaced with constructor-injected collaborators, but the original
/// shape of the computation, a running subtotal built up statement by
/// statement, left intact.
/// </summary>
public sealed class RefactoredRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine
{
    /// <summary>
    /// Quotes a rate the same way the legacy engine did, statement by
    /// statement, but through the injected <see cref="ITariffSource"/> and
    /// <see cref="IClock"/> instead of a hard-coded tariff table and the
    /// ambient wall clock.
    /// </summary>
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = request.DeclaredValue * RateRules.DeclaredValueRate;
        subtotal += request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode);

        var at = clock.UtcNow;
        if (PeakWindow.IsPeak(at))
        {
            subtotal *= 1m + RateRules.PeakMultiplier;
        }

        return new RateQuote(request.ConsignmentId, subtotal, at);
    }
}
