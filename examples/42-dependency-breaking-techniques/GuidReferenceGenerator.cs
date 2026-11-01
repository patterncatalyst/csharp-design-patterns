namespace RateEngineDependencies;

/// <summary>
/// The production <see cref="IReferenceGenerator"/>, backed by a new
/// <see cref="Guid"/> in its compact "N" (32 hex characters, no separators)
/// format.
/// </summary>
public sealed class GuidReferenceGenerator : IReferenceGenerator
{
    public string NewReference() => Guid.NewGuid().ToString("N");
}
