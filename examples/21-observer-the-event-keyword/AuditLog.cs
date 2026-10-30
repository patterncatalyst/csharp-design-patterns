namespace ConsignmentTracking;

/// <summary>
/// A second, independent subscriber to the same <see cref="ConsignmentTracker"/>
/// event. It keeps every handling event it has seen for later audit, proving
/// that a single reported event reaches every attached handler, not just the
/// first one wired up.
/// </summary>
public sealed class AuditLog
{
    private readonly List<HandlingEvent> _entries = [];

    public IReadOnlyList<HandlingEvent> Entries => _entries;

    public void OnHandlingReported(object? sender, HandlingReportedEventArgs e)
    {
        _entries.Add(e.Handling);
    }
}
