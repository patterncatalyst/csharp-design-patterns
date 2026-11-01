namespace ConcurrentHandling;

/// <summary>
/// Observes handling events raised by a <see cref="HandlingEventHub"/>. An
/// explicit interface, not the <c>event</c> keyword, is the observer contract
/// here: a multicast delegate field invokes its subscribers through a mutable
/// invocation list that is not safe to read concurrently with a subscribe or
/// unsubscribe, which is exactly the race this chapter is about avoiding.
/// </summary>
public interface IHandlingObserver
{
    void OnHandling(HandlingEvent e);
}
