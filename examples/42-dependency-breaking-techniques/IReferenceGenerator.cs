namespace RateEngineDependencies;

/// <summary>
/// Generates a quote reference. Extracting this interface breaks
/// <see cref="RateEngine"/>'s dependency on the ambient
/// <see cref="Guid.NewGuid"/>, so a test can assert against a known
/// reference instead of an unpredictable one.
/// </summary>
public interface IReferenceGenerator
{
    string NewReference();
}
