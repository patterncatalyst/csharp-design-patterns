using System.Collections.Concurrent;
using System.Threading.Channels;

namespace HandlingEventIngest;

/// <summary>
/// A bounded Producer-Consumer pipeline for handling-event ingest, built over
/// <see cref="System.Threading.Channels.Channel{T}"/>: the pattern is
/// Producer-Consumer, the mechanism is a bounded channel with a fixed number
/// of consumer tasks draining it. A full channel makes <see cref="SubmitAsync"/>
/// wait rather than growing without bound, which is the backpressure this
/// example exists to demonstrate. Calling <see cref="CompleteAndDrainAsync"/>
/// completes the channel's writer and awaits every consumer, so every
/// in-flight event is processed before the pipeline reports its results; no
/// wall-clock wait is ever needed to know the pipeline is done.
/// </summary>
public sealed class HandlingEventPipeline
{
    private readonly Channel<HandlingEvent> _channel;
    private readonly Func<HandlingEvent, CancellationToken, ValueTask> _onEvent;
    private readonly ConcurrentQueue<HandlingEvent> _observed;
    private int _processed;
    private readonly List<Task> _consumers;

    /// <summary>
    /// Creates the bounded channel and starts <paramref name="consumerCount"/>
    /// consumer tasks draining it immediately. <paramref name="onEvent"/> is
    /// the per-event processing hook; the default simply yields once, so a
    /// caller that only cares about throughput and ordering does not have to
    /// supply one. Tests that need to observe or gate consumption, such as
    /// proving backpressure, supply their own hook.
    /// </summary>
    public HandlingEventPipeline(int capacity, int consumerCount, Func<HandlingEvent, CancellationToken, ValueTask>? onEvent = null)
    {
        _channel = Channel.CreateBounded<HandlingEvent>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });
        _onEvent = onEvent ?? (static async (_, _) => await Task.Yield());
        _observed = new ConcurrentQueue<HandlingEvent>();
        _consumers = new List<Task>(consumerCount);
        for (var i = 0; i < consumerCount; i++)
        {
            _consumers.Add(ConsumeAsync());
        }
    }

    /// <summary>
    /// Submits one handling event to the channel, waiting if the channel is
    /// currently full rather than dropping the event or growing an unbounded
    /// buffer. Honours <paramref name="ct"/>: a token cancelled before or
    /// during the wait makes this throw instead of writing.
    /// </summary>
    public ValueTask SubmitAsync(HandlingEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        return _channel.Writer.WriteAsync(e, ct);
    }

    /// <summary>
    /// Signals that no further events will be submitted, then waits for every
    /// consumer to finish draining the channel before summarizing what they
    /// processed. This is the deterministic alternative to a wall-clock wait:
    /// the pipeline is done exactly when every consumer task completes.
    /// </summary>
    public async Task<IngestReport> CompleteAndDrainAsync()
    {
        _channel.Writer.Complete();
        await Task.WhenAll(_consumers);

        var observed = _observed.ToArray();
        var byKind = observed
            .GroupBy(e => e.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

        return new IngestReport(_processed, observed, byKind);
    }

    /// <summary>
    /// Drains the channel until its writer is completed and every already
    /// buffered event has been read, invoking the processing hook for each
    /// one before recording it as observed.
    /// </summary>
    private async Task ConsumeAsync()
    {
        await foreach (var e in _channel.Reader.ReadAllAsync())
        {
            await _onEvent(e, CancellationToken.None);
            _observed.Enqueue(e);
            Interlocked.Increment(ref _processed);
        }
    }
}
