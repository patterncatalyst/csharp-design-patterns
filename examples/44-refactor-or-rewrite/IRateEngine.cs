namespace RateEngineReplacement;

/// <summary>
/// The stable seam neither the legacy engine nor its replacements can see
/// each other through: a caller bound to this contract, such as
/// <see cref="QuoteService"/>, can swap the refactored implementation for the
/// rewritten one, or back again, without a source change of its own. The
/// legacy engine deliberately does not implement this interface; it has no
/// seam to implement it through.
/// </summary>
public interface IRateEngine
{
    RateQuote Quote(RateRequest request);
}
