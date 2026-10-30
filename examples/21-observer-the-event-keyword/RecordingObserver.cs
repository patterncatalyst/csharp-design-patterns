namespace ConsignmentTracking;

/// <summary>
/// An <see cref="IObserver{T}"/> that records every notification it
/// receives from a <see cref="HandlingEventStream"/>: the handling events
/// delivered through <see cref="OnNext"/>, whether the sequence completed,
/// and any error it was terminated with.
/// </summary>
public sealed class RecordingObserver : IObserver<HandlingEvent>
{
    private readonly List<HandlingEvent> _received = [];

    public IReadOnlyList<HandlingEvent> Received => _received;

    public bool Completed { get; private set; }

    public Exception? Error { get; private set; }

    public void OnNext(HandlingEvent value)
    {
        _received.Add(value);
    }

    public void OnCompleted()
    {
        Completed = true;
    }

    public void OnError(Exception error)
    {
        Error = error;
    }
}
