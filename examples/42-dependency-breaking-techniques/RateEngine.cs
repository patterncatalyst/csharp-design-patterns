namespace RateEngineDependencies;

/// <summary>
/// Quotes a consignment over a lane. Every dependency the
/// <see cref="Legacy.RateEngine"/> baked in is broken here through a seam: a
/// tariff book, an audit log, a clock, and a reference generator all arrive
/// through the constructor (constructor injection), the caller's identity
/// arrives as a parameter instead of <see cref="Environment.UserName"/>
/// (Parameterize Method), and reference generation is also reachable through
/// an overridable seam (Extract-and-Override) for a caller that cannot wire
/// in an <see cref="IReferenceGenerator"/>. Not sealed, so that seam stays
/// open to a subclass.
/// </summary>
public class RateEngine(ITariffBook tariffs, IAuditLog audit, IClock clock, IReferenceGenerator references)
{
    /// <summary>
    /// Quotes <paramref name="request"/> on behalf of <paramref name="quotedBy"/>.
    /// Throws <see cref="UnknownLaneException"/> if the request names a lane
    /// <see cref="LaneValidation"/> does not recognize.
    /// </summary>
    public RateQuote Quote(RateRequest request, string quotedBy)
    {
        if (!LaneValidation.IsKnownLane(request.LaneCode))
        {
            throw new UnknownLaneException($"Unknown lane code '{request.LaneCode}'.");
        }

        var rate = tariffs.RateFor(request.LaneCode);
        var amount = rate * request.WeightKilograms;
        var reference = NewReference();
        var at = clock.UtcNow;

        audit.Record($"Quoted {reference} for consignment {request.ConsignmentId}: {amount:0.00}");

        return new RateQuote(reference, request.ConsignmentId, amount, quotedBy, at);
    }

    /// <summary>
    /// Generates the reference for a quote. Delegates to the injected
    /// <see cref="IReferenceGenerator"/> by default; a subclass can override
    /// this seam directly (Extract-and-Override) when a known reference
    /// matters more than substituting the generator itself.
    /// </summary>
    protected virtual string NewReference() => references.NewReference();
}
