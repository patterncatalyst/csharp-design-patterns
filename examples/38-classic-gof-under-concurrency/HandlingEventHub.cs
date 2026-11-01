using System.Collections.Immutable;

namespace ConcurrentHandling;

/// <summary>
/// Publishes <see cref="HandlingEvent"/> instances to every subscribed
/// <see cref="IHandlingObserver"/>. Subscribers are held in an
/// <see cref="ImmutableArray{T}"/> swapped in with
/// <see cref="ImmutableInterlocked.Update{T,TArg}(ref ImmutableArray{T}, System.Func{ImmutableArray{T}, TArg, ImmutableArray{T}}, TArg)"/>,
/// so a subscribe or unsubscribe never mutates the list a concurrent
/// <see cref="Raise"/> is iterating. <see cref="Raise"/> captures one
/// reference to the current array before looping; that snapshot cannot change
/// underneath the loop no matter what another thread does to the field
/// afterward, because <c>Add</c> and <c>Remove</c> on an immutable array
/// return a new array rather than editing the one in hand.
/// </summary>
public sealed class HandlingEventHub
{
    private ImmutableArray<IHandlingObserver> _observers = [];

    public IDisposable Subscribe(IHandlingObserver observer)
    {
        ImmutableInterlocked.Update(ref _observers, static (list, o) => list.Add(o), observer);
        return new Subscription(this, observer);
    }

    public void Raise(HandlingEvent e)
    {
        var snapshot = _observers;
        foreach (var o in snapshot)
        {
            o.OnHandling(e);
        }
    }

    public int ObserverCount => _observers.Length;

    private sealed class Subscription(HandlingEventHub hub, IHandlingObserver observer) : IDisposable
    {
        public void Dispose() =>
            ImmutableInterlocked.Update(ref hub._observers, static (list, o) => list.Remove(o), observer);
    }
}
