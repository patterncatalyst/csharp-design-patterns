namespace DocumentDispatch;

/// <summary>
/// Thrown by <see cref="DocumentDispatcher.Render"/> when asked to render a
/// document kind with no registered handler. <see cref="DocumentDispatcher.TryRender"/>
/// is the non-throwing alternative for a caller that expects misses.
/// </summary>
public sealed class UnknownDocumentKindException : Exception
{
    public UnknownDocumentKindException()
    {
    }

    public UnknownDocumentKindException(string message) : base(message)
    {
    }

    public UnknownDocumentKindException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
