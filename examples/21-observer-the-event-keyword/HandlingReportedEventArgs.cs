namespace ConsignmentTracking;

/// <summary>
/// The payload delivered to every subscriber of
/// <see cref="ConsignmentTracker.HandlingReported"/>, following the
/// standard <c>EventHandler&lt;TEventArgs&gt;</c> shape: a
/// <see cref="HandlingEvent"/> wrapped in a dedicated
/// <see cref="EventArgs"/> subclass instead of passed as a bare argument.
/// </summary>
public sealed class HandlingReportedEventArgs(HandlingEvent handling) : EventArgs
{
    public HandlingEvent Handling { get; } = handling;
}
