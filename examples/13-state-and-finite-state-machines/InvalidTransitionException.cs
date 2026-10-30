namespace ConsignmentTracking;

/// <summary>
/// Thrown by <see cref="Transitions.Next"/> when a <see cref="HandlingEventType"/>
/// does not have a legal transition out of the current <see cref="ConsignmentStatus"/>,
/// for example delivering a consignment that has not yet cleared customs.
/// </summary>
public sealed class InvalidTransitionException : Exception
{
    public InvalidTransitionException()
    {
    }

    public InvalidTransitionException(string message)
        : base(message)
    {
    }

    public InvalidTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
