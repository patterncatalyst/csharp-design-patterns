namespace ConcurrentHandling.Tests;

/// <summary>
/// A test observer that disposes another observer's subscription the instant
/// it is itself notified. Used to prove that <see cref="HandlingEventHub.Raise"/>
/// iterates a fixed snapshot: an unsubscribe triggered mid-raise must not
/// affect who gets notified during that same raise, only the next one.
/// </summary>
public sealed class UnsubscribingObserver : IHandlingObserver
{
    private int _notifiedCount;

    public IDisposable? SubscriptionToDispose { get; set; }

    public int NotifiedCount => Volatile.Read(ref _notifiedCount);

    public void OnHandling(HandlingEvent e)
    {
        Interlocked.Increment(ref _notifiedCount);
        SubscriptionToDispose?.Dispose();
    }
}
