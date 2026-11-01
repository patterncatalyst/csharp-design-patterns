namespace RateEngineDependencies.Tests;

/// <summary>
/// Exercises the Extract-and-Override seam on <see cref="RateEngine"/>: it
/// overrides <see cref="RateEngine.NewReference"/> directly instead of
/// substituting an <see cref="IReferenceGenerator"/>, proving the seam is
/// reachable even when a caller has no generator to inject.
/// </summary>
public sealed class FixedReferenceEngine(ITariffBook tariffs, IAuditLog audit, IClock clock, IReferenceGenerator references)
    : RateEngine(tariffs, audit, clock, references)
{
    protected override string NewReference() => "RQ-FIXED-0001";
}
