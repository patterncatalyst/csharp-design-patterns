namespace ConsignmentRouting;

/// <summary>
/// Thrown by <see cref="PortCodeRegistry.Get"/> when asked for a code the
/// registry never interned. <see cref="PortCodeRegistry.TryGet"/> is the
/// non-throwing alternative for a caller that expects misses.
/// </summary>
public sealed class UnknownPortCodeException : Exception
{
    public UnknownPortCodeException()
    {
    }

    public UnknownPortCodeException(string message) : base(message)
    {
    }

    public UnknownPortCodeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
