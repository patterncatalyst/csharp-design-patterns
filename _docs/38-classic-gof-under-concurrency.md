---
title: "Classic GoF under concurrency"
order: 38
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "Observer across threads, Iterator as IAsyncEnumerable, Decorator over async, Command as Task, and immutability as a concurrency strategy."
---

<!-- EXAMPLE (shipping domain): Handling events observed across threads; the event feed as IAsyncEnumerable. -->

## Learning objectives

By the end of this chapter you can:

- Explain why `HandlingEventHub` keeps subscribers in an `ImmutableArray<IHandlingObserver>` updated through `ImmutableInterlocked.Update`, and what that guarantees `Raise` when someone subscribes or unsubscribes mid-notification.
- Trace how `async IAsyncEnumerable<HandlingEvent> StreamAsync` and `await foreach` make `HandlingEventFeed` an Iterator over a sequence still being written, and how `[EnumeratorCancellation]` wires in a caller's token.
- Keep `CountingValidatorDecorator` a Decorator under `async` by awaiting the inner call, and say why its counter needs `Interlocked.Increment`.
- Run a batch of `IHandlingCommand` objects concurrently through `CommandInvoker.RunAllAsync` with `Select` and `Task.WhenAll`.
- State what declaring `HandlingEvent` as a record guarantees when one event is read from many threads.

## Why this matters

The 1994 catalogue describes Observer, Iterator, Decorator and Command as single-threaded collaborations: one caller walks the subscribers, steps through the aggregate, or calls the wrapped method and waits for the result. In modern C#, observers subscribe and unsubscribe while a raise is in flight, a producer and a consumer share a feed, many callers await one decorator, and an invoker starts every command before any finishes. Each pattern keeps its shape. What changes is how it stays safe.

That doesn't mean adding locks. C# already ships the primitives: an immutable collection swapped instead of mutated, an async iterator compiled into a state machine, a `Task`-returning method a decorator can await, and a command whose result is a `Task`. All four patterns, plus the immutability under them, are built here around a consignment moving through receipt, loading, unloading, customs and claim.

{% include excalidraw.html name="38-classic-gof-under-concurrency" caption="Figure 38.1: Four classic patterns, observer, iterator, decorator, and command, adapt to concurrency around one immutable handling event that every caller shares safely." %}

## Pattern: Observer

### The problem

Observers subscribe and unsubscribe on other threads while `Raise` walks the list. A raiser needs to know what it may assume about that list once a notification starts.

### The solution

**An Observer lets a subject notify any number of dependents of a change without knowing their concrete types.**

### How C# models it

`IHandlingObserver` is an interface with one method, `OnHandling(HandlingEvent e)`, rather than an `event`. The compiler-generated `+=` and `-=` accessors on a field-like event already make subscribe and unsubscribe thread-safe. What an `event` doesn't state is what a raiser may assume about the list it's walking. `HandlingEventHub` makes that a named, checkable property:

```csharp
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
}
```

`Subscribe` never edits the array. `ImmutableInterlocked.Update` reads the current array, applies `list.Add(o)` to produce a new one, and compare-and-swaps the field, retrying if another thread changed it first. `Add` and `Remove` on an immutable array always return a new array, so a retry can't corrupt a version someone else is reading.

### The snapshot guarantee

`Raise` reads `_observers` once into `snapshot` and never touches the field again. A concurrent subscribe or unsubscribe swaps the field, not the array already handed out. So:

- an observer that disposes another observer's subscription from inside `OnHandling` changes who's notified on the next raise; the other observer still receives the in-flight event;
- thirty-two raisers released through a `Barrier` deliver every event to the one subscribed observer exactly once.

{% include excalidraw.html name="38-observer-immutable-snapshot" caption="Figure 38.2: HandlingEventHub captures one immutable snapshot of subscribers before Raise iterates it, while a concurrent subscribe or unsubscribe swaps in a new snapshot without disturbing the notification already in flight." %}

## Pattern: Iterator

### The problem

The classic Iterator walks an aggregate that's complete before anyone reads it. A handling feed is still being written while a consumer reads it.

### The solution

**An Iterator gives sequential access to the elements of an aggregate without exposing how it's stored.**

### How C# models it

`HandlingEventFeed` keeps the `foreach` traversal and drops the assumption that the aggregate is finished:

```csharp
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
```

The compiler turns `StreamAsync` into a state machine implementing `IAsyncEnumerator<HandlingEvent>`, the async counterpart of an `IEnumerable<T>` iterator. Each `yield return` hands one event to the awaiting consumer and parks until `MoveNextAsync` is called again. When no event is ready, the state machine suspends and the thread returns to the pool until `PublishAsync` writes or `Complete` closes the writer.

### Cancellation and concurrent reading

`[EnumeratorCancellation]` marks the parameter that receives the token passed to `GetAsyncEnumerator(CancellationToken)`, so a token given at the `await foreach` site reaches `ReadAllAsync(ct)` instead of being ignored. An already-cancelled token throws `OperationCanceledException` before any event is read.

{% include excalidraw.html name="38-async-iterator-feed" caption="Figure 38.3: Producers publish handling events into the feed while a consumer drains it with an await foreach loop. Calling Complete ends the sequence cleanly, and a cancelled token ends it early." %}

Events published and then completed come back in publication order. A producer and a consumer running as separate tasks over one feed see every event consumed exactly once. `Channel<T>`'s writer and reader are built for concurrent use, so the feed needs no lock; it only exposes that behavior through the iterator shape.

## Pattern: Decorator

### The problem

Counting validation calls means wrapping `IManifestValidator`, and the wrapped method is asynchronous and called by many callers at once.

### The solution

**A Decorator adds behavior to an object by wrapping it in another that implements the same interface and forwards each call.**

### How C# models it

```csharp
public sealed class CountingValidatorDecorator(IManifestValidator inner) : IManifestValidator
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public async Task<bool> ValidateAsync(HandlingEvent e, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        return await inner.ValidateAsync(e, ct);
    }
}
```

The one difference from a synchronous decorator: it awaits `inner.ValidateAsync` instead of discarding the task or blocking on `.Result` or `.Wait()`. The decorator suspends when the inner validator does and resumes with its result. `ManifestValidator` stands in for a manifest lookup or database read with `await Task.Yield()`, with no wall-clock delay.

### Protecting the decorator's own state

The counter is the only concurrent concern. Many callers can be inside `ValidateAsync` on one instance, so `_calls` uses `Interlocked.Increment` and `Volatile.Read` rather than `_calls++` and a plain read. Sixty-four concurrent calls through `Task.WhenAll` leave `Calls` at exactly 64, each with the validator's real result. Wrapping an async component changes nothing else: await instead of call, and protect the decorator's own state.

## Pattern: Command

### The problem

A batch of handling requests should run concurrently, with each result matched back to its request and no two colliding on the ledger.

### The solution

**A Command packages a request as an object, so it can be queued, batched or run by an invoker that doesn't know what it does.**

### How C# models it

`IHandlingCommand.ExecuteAsync(CancellationToken ct)` returns `Task<HandlingReceipt>` instead of the classic `void Execute()`. `RecordHandlingCommand` closes over one `HandlingEvent` and the `IHandlingLedger` receiver:

```csharp
public sealed class RecordHandlingCommand(HandlingEvent evt, IHandlingLedger ledger) : IHandlingCommand
{
    public async Task<HandlingReceipt> ExecuteAsync(CancellationToken ct)
    {
        var seq = await ledger.AppendAsync(evt, ct);
        return new HandlingReceipt(evt.Tracking, seq);
    }
}
```

`InMemoryHandlingLedger.AppendAsync` assigns sequence numbers with `Interlocked.Increment` and appends under a `lock`, so concurrent commands never share a sequence number or corrupt the list. That lets the invoker treat a batch as independent work:

```csharp
public sealed class CommandInvoker
{
    private int _totalCommandsRun;

    public int TotalCommandsRun => Volatile.Read(ref _totalCommandsRun);

    public async Task<IReadOnlyList<HandlingReceipt>> RunAllAsync(
        IReadOnlyCollection<IHandlingCommand> commands, CancellationToken ct)
    {
        var pending = commands.Select(c => c.ExecuteAsync(ct)).ToList();
        var receipts = await Task.WhenAll(pending);
        Interlocked.Add(ref _totalCommandsRun, receipts.Length);
        return receipts;
    }
}
```

### Running a batch concurrently

- **`Select` starts every command**, because calling an `async` method runs it to its first await.
- **`ToList()` materializes the started tasks**; a lazy sequence would start each command again on every enumeration.
- **`Task.WhenAll` returns results in input order**, so a receipt matches its command by position.
- **`TotalCommandsRun` uses `Interlocked.Add`**, because the continuation after `WhenAll` can run on any thread.

Twenty commands through `RunAllAsync` return twenty receipts whose sequence numbers are distinct and gap-free.

## Immutability as a concurrency strategy

All four patterns rely on one decision:

```csharp
public sealed record HandlingEvent(TrackingId Tracking, HandlingKind Kind, int Sequence);
```

A record's positional properties are init-only, so a constructed `HandlingEvent` never changes. A changed version comes from a `with` expression, `original with { Sequence = 7 }`, which leaves the original alone. The same event flows through `HandlingEventHub.Raise`, `HandlingEventFeed`'s channel, `CountingValidatorDecorator.ValidateAsync` and `InMemoryHandlingLedger.AppendAsync`, possibly on different threads, with nothing to coordinate: a value nobody can mutate can't be read torn.

`TrackingId` is a `readonly record struct`, so each reader copies it by value and no two threads alias one mutable instance. Sixteen readers released through a `Barrier` each see the original fields intact, each make their own `with` copy, and the original is unchanged afterward. Immutability is why none of the four patterns locks the event data; the locks and interlocked operations guard only each pattern's bookkeeping: subscriber lists, sequence counters, call counts.

## Build, run, observe

```
cd examples/38-classic-gof-under-concurrency && dotnet test
```

All 10 tests pass: three for Observer (single-threaded, thirty-two concurrent raisers, unsubscribe mid-raise), three for Iterator (order, cancellation, concurrent producer and consumer), one for Decorator (sixty-four concurrent calls), two for Command (one receipt, a twenty-command batch) and one for immutability (sixteen readers). `Barrier` instances synchronize the concurrent starts, and nothing depends on wall-clock timing.

## When not to use it

The question in every case is whether more than one thread touches the structure at overlapping times. When it doesn't, write the single-threaded version.

| Adaptation | Skip it when |
| --- | --- |
| Immutable snapshot of observers | Every subscribe and raise happens on one thread; a `List<IHandlingObserver>` or the `event` keyword costs less |
| Channel-backed `IAsyncEnumerable<T>` | The sequence is complete before anyone reads it; return `IEnumerable<T>` |
| `Interlocked` counter in a decorator | Only one thread ever calls through it; a plain increment is correct and faster |
| Records copied with `with` | A hot path stays on one thread; each copy is an allocation that mutating a field avoids |

<blockquote class="ai-sidebar" markdown="1">
Once the decision to make a pattern thread-safe is made, an assistant drafts the pieces well: a correct `ImmutableInterlocked.Update` call, `[EnumeratorCancellation]` on an async iterator's token, `Interlocked.Increment` on a decorator's counter. Whether the code runs under concurrency at all, and which strategy fits (an immutable snapshot, a channel, an interlocked counter, or nothing), stays a human judgment. Asked to "make this observer thread-safe," an assistant often adds synchronization unconditionally, because a lock or an immutable collection looks safe whether or not two threads will ever meet there.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480/9780321700698) (Addison-Wesley, 1994): the source catalogue for Observer, Iterator, Decorator, and Command, the four patterns re-expressed here for concurrent, asynchronous C#.
- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue that names the concurrency forces, safe publication, immutable sharing, and producer/consumer coordination, that these re-expressed patterns resolve.
- Microsoft Learn, [Iterators](https://learn.microsoft.com/en-us/dotnet/csharp/iterators) and [Generate and consume async streams](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream): the authority for the `IAsyncEnumerable<T>` state machine and the `[EnumeratorCancellation]` attribute `HandlingEventFeed` relies on.
- Microsoft Learn, [Immutable collections](https://learn.microsoft.com/en-us/dotnet/standard/collections/immutable): the reference for `ImmutableArray<T>` and `ImmutableInterlocked`, the primitives behind `HandlingEventHub`'s subscriber list.
- Microsoft Learn, [Handle and raise events](https://learn.microsoft.com/en-us/dotnet/standard/events/): the reference for the `event` keyword's own thread-safe add and remove accessors, which `IHandlingObserver` complements with an explicit raise-time guarantee.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
