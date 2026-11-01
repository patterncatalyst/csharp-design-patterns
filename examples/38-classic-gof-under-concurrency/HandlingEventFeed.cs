using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ConcurrentHandling;

/// <summary>
/// Iterates a stream of <see cref="HandlingEvent"/> instances as they are
/// published, rather than over a materialized collection. The classic
/// Iterator pattern walks a fixed, already-complete aggregate; this feed
/// exposes the same <c>foreach</c>-shaped traversal over an aggregate that is
/// still being written, by pairing an unbounded <see cref="Channel{T}"/> with
/// <see cref="IAsyncEnumerable{T}"/>: <see cref="StreamAsync"/> suspends
/// between items instead of returning early, and resumes the instant the next
/// event is published or <see cref="Complete"/> is called.
/// </summary>
public sealed class HandlingEventFeed
{
    private readonly Channel<HandlingEvent> _channel = Channel.CreateUnbounded<HandlingEvent>();

    public ValueTask PublishAsync(HandlingEvent e, CancellationToken ct) =>
        _channel.Writer.WriteAsync(e, ct);

    public void Complete() => _channel.Writer.Complete();

    public async IAsyncEnumerable<HandlingEvent> StreamAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var e in _channel.Reader.ReadAllAsync(ct))
        {
            yield return e;
        }
    }
}
