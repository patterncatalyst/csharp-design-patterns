namespace RateEngineDependencies;

/// <summary>
/// The production <see cref="IAuditLog"/>. Unlike <see cref="Legacy.LegacyAuditLog"/>,
/// its entries are exposed through <see cref="Entries"/>, so a caller that
/// holds the instance (production code or a test) can read back what was
/// recorded.
/// </summary>
public sealed class ListAuditLog : IAuditLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Record(string message) => _entries.Add(message);
}
