using System.Diagnostics.CodeAnalysis;
using System.Reactive.Subjects;

namespace ConsignmentTracking;

/// <summary>
/// The reach-past-event shape of the same Observer relationship: a
/// <see cref="Subject{T}"/> from System.Reactive exposed as
/// <see cref="IObservable{T}"/>. Reach for this over a plain event when a
/// subscriber needs operator composition (filtering, buffering, combining
/// several streams), a uniform error channel, or an explicit completion
/// signal, none of which a bare <c>event</c> gives.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "HandlingEventStream names the Observer-over-IObservable<T> role for this chapter, " +
        "not an implementation of the System.IO.Stream contract; the name is fixed for this chapter.")]
public sealed class HandlingEventStream : IDisposable
{
    private readonly Subject<HandlingEvent> _subject = new();

    public IObservable<HandlingEvent> Events => _subject;

    public void Publish(HandlingEvent handling)
    {
        _subject.OnNext(handling);
    }

    public void Complete()
    {
        _subject.OnCompleted();
    }

    public void Dispose()
    {
        _subject.Dispose();
    }
}
