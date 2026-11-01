using System.Collections.Concurrent;

namespace ConcurrentHandling.Tests;

/// <summary>
/// A test observer that records how many times it was notified and every
/// event it saw, safely from any number of concurrent raisers: the count uses
/// <see cref="Interlocked"/>, and the recording uses a
/// <see cref="ConcurrentQueue{T}"/>.
/// </summary>
public sealed class CountingObserver : IHandlingObserver
{
    private readonly ConcurrentQueue<HandlingEvent> _received = [];
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public IReadOnlyCollection<HandlingEvent> Received => _received;

    public void OnHandling(HandlingEvent e)
    {
        Interlocked.Increment(ref _count);
        _received.Enqueue(e);
    }
}
