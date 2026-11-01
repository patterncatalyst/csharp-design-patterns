namespace RateEngineReplacement;

/// <summary>
/// The seam for a rate engine's notion of "now": an interface a caller can
/// substitute in a test, instead of the legacy shape's ambient
/// <see cref="DateTime.Now"/>.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
