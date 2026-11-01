using System.Diagnostics.CodeAnalysis;

namespace RateEngineDependencies.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: every
/// dependency this engine needs is baked in at the point of use, rather than
/// arriving through a seam. There is no constructor to inject a fake tariff
/// book, audit log, clock, reference generator, or caller into, so a test can
/// only run this exactly as it runs in production. See the refactored
/// <see cref="RateEngineDependencies.RateEngine"/> for the dependency-broken
/// form of this same engine.
/// </summary>
public sealed class RateEngine
{
    /// <summary>
    /// Quotes a consignment. The method bakes in five dependencies a test
    /// cannot see or replace: a newed-up <see cref="LegacyAuditLog"/>, the
    /// static <see cref="LegacyTariffBook"/>, <see cref="Guid.NewGuid"/>,
    /// <see cref="DateTime.UtcNow"/>, and <see cref="Environment.UserName"/>.
    /// </summary>
    /// <remarks>
    /// CA1822 (mark members as static) fires here because the method never
    /// touches instance state; that is the point of the Part-13 before/after
    /// exception, so it is scope-suppressed on this member rather than
    /// disabled for the assembly. The refactored form in
    /// <see cref="RateEngineDependencies.RateEngine"/> is an instance method
    /// for a real reason: it reads injected constructor dependencies.
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part-13 before/after exception: this method is intentionally shaped like the " +
            "untestable legacy code it contrasts with the refactored RateEngineDependencies.RateEngine, " +
            "which uses its instance state (injected collaborators) for real.")]
    public RateQuote Quote(RateRequest request)
    {
        var audit = new LegacyAuditLog();
        var rate = LegacyTariffBook.RateFor(request.LaneCode);
        var reference = Guid.NewGuid().ToString("N");
        var quotedAt = DateTime.UtcNow;
        var quotedBy = Environment.UserName;

        var amount = rate * request.WeightKilograms;
        audit.Record($"Quoted {reference} for consignment {request.ConsignmentId}: {amount:0.00}");

        return new RateQuote(reference, request.ConsignmentId, amount, quotedBy, quotedAt);
    }
}
