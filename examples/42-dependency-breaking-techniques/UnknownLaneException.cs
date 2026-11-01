namespace RateEngineDependencies;

/// <summary>
/// Thrown by <see cref="RateEngine.Quote"/> when a <see cref="RateRequest"/>
/// names a lane code <see cref="LaneValidation"/> does not recognize.
/// </summary>
public sealed class UnknownLaneException : Exception
{
    public UnknownLaneException()
    {
    }

    public UnknownLaneException(string message)
        : base(message)
    {
    }

    public UnknownLaneException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
