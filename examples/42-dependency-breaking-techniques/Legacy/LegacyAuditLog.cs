namespace RateEngineDependencies.Legacy;

/// <summary>
/// Intentionally untestable legacy shape shown for contrast: an audit log
/// that <see cref="RateEngine"/> news up for itself instead of receiving
/// through a constructor, so no test can observe what was recorded.
/// </summary>
public sealed class LegacyAuditLog
{
    private readonly List<string> _entries = [];

    /// <summary>
    /// Appends an audit message. Nothing outside this instance, and nothing
    /// that constructed it, can ever read <see cref="_entries"/> back.
    /// </summary>
    public void Record(string message) => _entries.Add(message);
}
