namespace RateEngineDependencies;

/// <summary>
/// Records an audit message for a quote. Extracting this interface breaks
/// <see cref="RateEngine"/>'s dependency on an audit log it would otherwise
/// construct for itself, so a test can verify what was recorded.
/// </summary>
public interface IAuditLog
{
    void Record(string message);
}
