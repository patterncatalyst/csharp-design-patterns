namespace RateEngineCatalogues;

/// <summary>
/// The Fowler stage: fully refactored over <see cref="SeamedRateEngine"/>.
/// The two collaborators are still constructor-injected, but the inline
/// string <c>switch</c> is gone, replaced by the <see cref="IServiceSurcharge"/>
/// strategy <see cref="ServiceSurcharges"/> selects, the inline rate literals
/// are gone, replaced by the named <see cref="RateRules"/> constants, and the
/// loose <c>(decimal, decimal, string)</c> signature is gone, replaced by a
/// single <see cref="RateRequest"/> that also carries the typed
/// <see cref="ServiceLevel"/>. Where the Feathers stage asks "what is the
/// smallest change that makes this testable", this stage asks "what is this
/// method actually doing, and what does its real shape look like".
/// </summary>
public sealed class RateEngine(ITariffSource tariffs, IClock clock)
{
    /// <summary>
    /// Quotes a rate for the given request: a base charge and a weight
    /// charge combine into a subtotal, the request's <see cref="ServiceLevel"/>
    /// selects a polymorphic surcharge over that subtotal, and the peak-hour
    /// surcharge is evaluated against the injected clock's current instant,
    /// which also stamps the returned quote.
    /// </summary>
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = BaseCharge(request) + WeightCharge(request);
        subtotal = ServiceSurcharges.For(request.Service).Apply(subtotal);

        var at = clock.UtcNow;
        var total = subtotal + PeakSurcharge(subtotal, at);

        return new RateQuote(request.ConsignmentId, total, at);
    }

    private static decimal BaseCharge(RateRequest request) => request.DeclaredValue * RateRules.DeclaredValueRate;

    private decimal WeightCharge(RateRequest request) => request.WeightKilograms * tariffs.RatePerKilogram(RateRules.StandardLane);

    private static decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) =>
        PeakWindow.IsPeak(at) ? subtotal * RateRules.PeakMultiplier : 0m;
}
