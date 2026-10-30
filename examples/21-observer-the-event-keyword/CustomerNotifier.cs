namespace ConsignmentTracking;

/// <summary>
/// A subscriber that would send a customer an update for every handling
/// event. It records each one it receives instead of sending anything, so
/// a test can assert on delivery order without faking a mail or SMS
/// gateway.
/// </summary>
public sealed class CustomerNotifier
{
    private readonly List<HandlingEvent> _notifications = [];

    public IReadOnlyList<HandlingEvent> Notifications => _notifications;

    public void OnHandlingReported(object? sender, HandlingReportedEventArgs e)
    {
        _notifications.Add(e.Handling);
    }
}
