namespace RateEngineDependencies;

/// <summary>
/// Supplies the current time. Extracting this interface breaks
/// <see cref="RateEngine"/>'s dependency on the ambient
/// <see cref="DateTime.UtcNow"/>, so a test can stamp a quote with a fixed,
/// known instant.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
