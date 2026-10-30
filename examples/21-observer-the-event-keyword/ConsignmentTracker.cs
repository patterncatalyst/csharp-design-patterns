namespace ConsignmentTracking;

/// <summary>
/// The Observer subject, expressed as the CLR's built-in multicast event.
/// Any number of subscribers attach with <c>+=</c> and detach with
/// <c>-=</c>; <see cref="Report"/> raises the event through a
/// null-conditional invocation, so reporting a handling event with zero
/// subscribers attached never throws.
/// </summary>
public sealed class ConsignmentTracker
{
    private readonly List<HandlingEvent> _history = [];

    public event EventHandler<HandlingReportedEventArgs>? HandlingReported;

    public IReadOnlyList<HandlingEvent> History => _history;

    public void Report(HandlingEvent handling)
    {
        _history.Add(handling);
        HandlingReported?.Invoke(this, new HandlingReportedEventArgs(handling));
    }
}
