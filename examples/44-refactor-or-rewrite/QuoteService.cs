namespace RateEngineReplacement;

/// <summary>
/// A caller bound only to the <see cref="IRateEngine"/> seam, never to either
/// concrete engine. Swapping <see cref="RefactoredRateEngine"/> for
/// <see cref="RewrittenRateEngine"/>, or back again, is a one-line change at
/// the construction site and no change at all here.
/// </summary>
public sealed class QuoteService(IRateEngine engine)
{
    public RateQuote QuoteFor(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return engine.Quote(request);
    }
}
