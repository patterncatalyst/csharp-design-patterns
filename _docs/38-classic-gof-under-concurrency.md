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

- Explain why `HandlingEventHub` stores its subscribers in an `ImmutableArray<IHandlingObserver>` updated through `ImmutableInterlocked.Update`, rather than a mutable list guarded by a lock, and state exactly what guarantee that gives `Raise` when a subscribe or unsubscribe happens on another thread mid-notification.
- Trace how `async IAsyncEnumerable<HandlingEvent> StreamAsync` and `await foreach` let `HandlingEventFeed` behave like an Iterator over a sequence that is still being written, and how the `[EnumeratorCancellation]` attribute wires a caller's token into the compiler-generated state machine.
- Show how `CountingValidatorDecorator` stays a Decorator under `async` by awaiting the inner `IManifestValidator.ValidateAsync` call rather than blocking on it, and why its counter needs `Interlocked.Increment` even though it wraps only one method.
- Describe how `IHandlingCommand.ExecuteAsync` returning `Task<HandlingReceipt>` lets `CommandInvoker.RunAllAsync` run a whole batch of commands concurrently with `Select` and `Task.WhenAll`, and what `TotalCommandsRun` demonstrates about state an invoker keeps safely across calls.
- State why `HandlingEvent` is declared as a record, and what guarantee that gives every other pattern in this chapter when the same event value is read from more than one thread.

## Why this matters

The 1994 catalogue describes Observer, Iterator, Decorator, and Command as single-threaded collaborations: one caller walks a list of subscribers, one caller steps through an aggregate, one caller calls a wrapped method and gets a return value back before doing anything else. None of that assumes a second thread is doing the same thing at the same moment, because in the environment those patterns were written for, it usually was not. Modern C# runs handling events through an `HandlingEventHub` with any number of observers subscribing and unsubscribing while a raise is in flight, through an `HandlingEventFeed` that a producer and a consumer read and write at the same time, through a `CountingValidatorDecorator` that many callers await concurrently, and through a `CommandInvoker` that starts every command in a batch before any of them has finished. The shape of each pattern survives. What has to change is how each one proves it is safe.

This chapter is not about adding locks to old code. It is about recognizing that modern C# already ships the primitives these four patterns need to stay correct once more than one thread is involved, an immutable collection that swaps in a new version instead of mutating in place, an async iterator the compiler turns into a state machine, a `Task`-returning method a decorator can simply await, and a command whose result is itself a `Task` that an invoker can run alongside every other command in the batch. None of these are exotic. They are the ordinary C# 14 idiom for the job a classic pattern already names, and the handling-events example in this chapter builds all five ideas, Observer, Iterator, Decorator, Command, and the immutability that underwrites the other four, around one small domain: a consignment moving through receipt, loading, unloading, customs, and claim.

{% include excalidraw.html name="38-classic-gof-under-concurrency" caption="Figure 38.1: Four classic patterns, observer, iterator, decorator, and command, adapt to concurrency around one immutable handling event that every caller shares safely." %}

## Observer across threads

`IHandlingObserver` is a plain interface with one method, `OnHandling(HandlingEvent e)`, not the `event` keyword. That choice is deliberate, and it is worth being precise about why. The compiler-generated add and remove accessors behind `+=` and `-=` on a field-like event already protect the subscribe and unsubscribe operations themselves: combining or removing a delegate from the backing multicast field is a thread-safe operation on its own. What an `event` field does not make explicit is what a raiser is allowed to assume about the list it is about to walk once a raise has started. `HandlingEventHub` answers that question directly, in a shape a test can pin down.

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

`_observers` is an `ImmutableArray<IHandlingObserver>`, and `Subscribe` never edits it in place. `ImmutableInterlocked.Update` reads the current array, applies `list.Add(o)` to produce a brand-new array, and compares-and-swaps the field to that new array, retrying the whole read-apply-swap cycle if another thread changed the field first. `Add` and `Remove` on an immutable array always return a new array rather than touching the one a caller might still be holding, so the retry loop inside `Update` never corrupts a version another thread is reading.

`Raise` leans on exactly that property. It reads `_observers` once into `snapshot`, a single reference to whichever array happened to be current at that instant, and then loops over `snapshot`, never touching the field again. A subscribe or an unsubscribe that runs concurrently swaps in a different array for the *field*, but `snapshot` still points at the array that existed when the raise began, because swapping a field never reaches into an array already handed out and changes it underneath a reader. An observer that unsubscribes itself from inside `OnHandling`, mid-raise, changes who gets notified on the *next* raise, not the one already running, and the test suite proves exactly that: one observer disposes a second observer's subscription the instant it is notified, and the second observer still receives that same in-flight event, because `Raise` is already iterating a snapshot that the unsubscribe cannot touch. A second test runs thirty-two raisers released through a `Barrier` at once and checks that every event reaches the one subscribed observer exactly once, proving the snapshot read is correct under real concurrent raising, not just under a single thread calling `Raise` in a loop.

{% include excalidraw.html name="38-observer-immutable-snapshot" caption="Figure 38.2: HandlingEventHub captures one immutable snapshot of subscribers before Raise iterates it, while a concurrent subscribe or unsubscribe swaps in a new snapshot without disturbing the notification already in flight." %}

Using an explicit interface instead of `event` does not fix a hole in the language. It makes the raise-time guarantee a named, testable property of `HandlingEventHub` rather than an implicit consequence of how a multicast delegate field happens to behave, and it lets a test construct the exact race, an unsubscribe triggered from inside a notification, that a chapter on concurrency needs to demonstrate directly.

## Iterator as IAsyncEnumerable

The classic Iterator pattern walks a fixed, already-complete aggregate: build the collection, then hand a caller something that can step through it one element at a time. `HandlingEventFeed` keeps the same `foreach`-shaped traversal but drops the assumption that the aggregate is finished before anyone starts reading it.

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

`StreamAsync` is declared `async IAsyncEnumerable<HandlingEvent>`, which the compiler turns into a state machine implementing `IAsyncEnumerator<HandlingEvent>` behind the scenes, the asynchronous counterpart to the state machine an ordinary `IEnumerable<T>` iterator method already generates. Every `yield return e` is a suspension point: the state machine hands one event back to whoever is awaiting it and parks until `MoveNextAsync` is called again, which happens the moment a consumer's `await foreach` loop asks for the next item. Nothing about that loop blocks a thread while it waits. If the channel has no event ready yet, the state machine suspends and the thread goes back to the pool until `PublishAsync` writes something or `Complete` closes the writer.

`[EnumeratorCancellation]` on the `ct` parameter tells the compiler which parameter should receive the token a caller passes to the generated enumerator's `GetAsyncEnumerator(CancellationToken)` overload, so a token supplied at the `await foreach` call site flows all the way into the `ReadAllAsync(ct)` call inside the method body instead of being silently ignored. The test suite exercises that path directly: a feed built with an already-cancelled token throws `OperationCanceledException` out of the `await foreach` immediately, before a single event is read.

{% include excalidraw.html name="38-async-iterator-feed" caption="Figure 38.3: Producers publish handling events into the feed while a consumer drains it with an await foreach loop. Calling Complete ends the sequence cleanly, and a cancelled token ends it early." %}

Two more tests round out the pattern. One publishes a fixed run of events, calls `Complete`, and confirms `StreamAsync` yields them back in the exact order they were published, the ordering guarantee a reader expects from any iterator. The other runs a producer and a consumer as two separate tasks over the same feed at the same time, the producer publishing while the consumer is already inside its `await foreach` loop, and confirms every published event is consumed exactly once. `Channel<T>` is what makes that safe without any lock in `HandlingEventFeed` itself: the channel's own writer and reader are built to be used concurrently, so the feed's job is only to expose that behavior through the iterator shape a caller already knows.

## Decorator over async

`CountingValidatorDecorator` wraps an `IManifestValidator` with a call counter, and it does that the same way the classic Decorator pattern always has: implement the same interface the wrapped component implements, hold a reference to that component, and forward every call to it, adding behavior around the forwarding rather than inside the component itself.

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

The only part of this that differs from a synchronous decorator is that `ValidateAsync` returns `Task<bool>`, and the decorator honors that return type by awaiting `inner.ValidateAsync(e, ct)` rather than calling it and discarding the result, or worse, blocking on it with `.Result` or `.Wait()`. Awaiting the inner call means the decorator's own method suspends exactly when the inner validator does, and resumes with the inner result the moment it is available, which is what lets `ManifestValidator`'s own implementation stand in for asynchronous work, a manifest lookup, a database read, with a plain `await Task.Yield()` instead of depending on wall-clock delay.

The counter is the one piece of this class that has to think about concurrency at all. A single decorator instance can have many callers awaiting `ValidateAsync` at once, each one on its own path through the method, so `_calls` is incremented with `Interlocked.Increment` rather than `_calls++`, and read back through `Volatile.Read` rather than a plain field read, so no caller observes a half-written value. The decorator test drives sixty-four concurrent calls through `Task.WhenAll` and checks that `Calls` ends at exactly sixty-four and that every call got back the validator's real result, proving the decorator delegates every call exactly once even when every call overlaps with every other.

Nothing about wrapping an asynchronous component changes the shape of Decorator. The wrapper still implements the component's interface, still holds the component it wraps, and still adds its behavior around a single forwarded call. The only adjustment a developer has to make is awaiting instead of calling, and protecting whatever instance state the decorator itself keeps.

## Command as Task

`IHandlingCommand` packages a handling request as an object, with one method, `ExecuteAsync(CancellationToken ct)`, returning `Task<HandlingReceipt>` instead of the classic `void Execute()`. `RecordHandlingCommand` is the concrete command: it closes over one `HandlingEvent` and the `IHandlingLedger` that plays the role of receiver, and its `ExecuteAsync` delegates straight to the ledger.

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

`InMemoryHandlingLedger.AppendAsync` assigns the sequence number with `Interlocked.Increment` and appends under a plain `lock` around the backing list, so many commands can call `AppendAsync` concurrently without two of them ever receiving the same sequence number or corrupting the list. That is what makes it safe for `CommandInvoker` to treat a batch of commands as independent work rather than a queue to drain one at a time.

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

`Select(c => c.ExecuteAsync(ct))` starts every command's `ExecuteAsync` immediately, because calling an `async Task`-returning method begins running it up to its first await before the caller does anything else with the resulting `Task`. `ToList()` materializes that `Select` into a concrete list of already-started tasks rather than leaving it as a lazy sequence that would restart each command every time it was enumerated. `Task.WhenAll(pending)` then awaits every one of those tasks together, resuming once the slowest command finishes, and returns their results as a single array in the same order the commands were passed in, so a caller can still match a receipt back to the command that produced it by position. The invoker's own `TotalCommandsRun` counter, updated with `Interlocked.Add` after the batch completes, is ordinary instance state that has to survive being touched by whatever thread each `WhenAll` continuation happens to run on, which is exactly why it goes through the same interlocked pattern as the ledger's sequence counter and the decorator's call counter rather than a plain increment.

The command test suite confirms both halves of this. One test proves a single command records its event and returns a receipt carrying the right tracking identifier and a valid sequence number. The other runs twenty commands through `RunAllAsync` at once and checks that the invoker returns exactly twenty receipts, each with a distinct sequence number, and that those sequence numbers form a gap-free run, proof that running every command concurrently never caused two of them to collide on the ledger's sequence counter.

## Immutability as a concurrency strategy

Every pattern above leans on one more decision: `HandlingEvent` is a `sealed record`.

```csharp
public sealed record HandlingEvent(TrackingId Tracking, HandlingKind Kind, int Sequence);
```

A record's positional properties are init-only by default, so once a `HandlingEvent` is constructed, nothing can change `Tracking`, `Kind`, or `Sequence` on that instance again. A thread that wants a changed version builds one with a `with` expression, `original with { Sequence = 7 }`, which produces a new record and leaves the original completely untouched. That single property is what lets the same `HandlingEvent` flow through `HandlingEventHub.Raise`, sit inside `HandlingEventFeed`'s channel, pass through `CountingValidatorDecorator.ValidateAsync`, and get appended by `InMemoryHandlingLedger.AppendAsync`, all potentially from different threads, without any of those four needing to coordinate access to the event itself. There is nothing to coordinate: a value nobody can mutate cannot be read in a torn or half-written state, no matter how many threads hold a reference to it at once.

`TrackingId` reinforces the same idea from a different angle. It is a `readonly record struct`, so every thread that reads a `TrackingId` copies it by value rather than sharing a reference to one mutable instance, which rules out a whole class of aliasing bugs before they can occur. The immutability test in the suite makes the guarantee concrete rather than asserting it as a claim: sixteen readers are released through a `Barrier` at the same instant, every reader asserts the original event's fields are exactly what they were at construction, each reader produces its own `with`-copy with a distinct sequence number, and after every reader finishes, the original event is checked once more and is still unchanged. Immutability is not a separate pattern sitting alongside Observer, Iterator, Decorator, and Command in this chapter. It is the property that makes sharing a single `HandlingEvent` across all four of them safe in the first place, and it is the reason none of those four needed a lock around the event data itself, only around the bookkeeping, subscriber lists, sequence counters, call counts, each pattern keeps on top of it.

## Build, run, observe

```
cd examples/38-classic-gof-under-concurrency && dotnet test
```

```
Test run summary: Passed!
  total: 10
  failed: 0
  succeeded: 10
  skipped: 0
```

The suite covers all five ideas in this chapter: two Observer tests, one single-threaded and one driven by thirty-two raisers released through a `Barrier`, plus a third that proves an unsubscribe triggered mid-raise never disturbs the in-flight snapshot; three Iterator tests covering in-order delivery, a cancelled token, and a concurrent producer and consumer over one feed; one Decorator test driving sixty-four concurrent calls through the counter; two Command tests covering a single command's receipt and a twenty-command batch run through the invoker; and one immutability test sharing a single event across sixteen concurrent readers. Every test is deterministic: the `Barrier` instances synchronize concurrent starts precisely, and nothing in the suite depends on wall-clock timing to pass.

## When not to use it

None of these four re-expressions is free, and a chapter about adapting classic patterns to concurrency should be equally clear about when the adaptation is not worth making. An immutable snapshot swap on every subscribe and unsubscribe is overhead a purely single-threaded observer list does not need: if every subscription and every raise happens on one thread, a plain `List<IHandlingObserver>` costs less to maintain and read than an `ImmutableArray<T>` rebuilt on every change, and the `event` keyword's own thread-safe add and remove accessors are already more than sufficient. An `IAsyncEnumerable<T>` feed backed by a channel is solving a problem a method that returns `IEnumerable<T>` over an already-complete list does not have; reach for the channel-backed version only when the sequence is still being produced while something else is already consuming it. A decorator that never awaits anything does not need `Interlocked` bookkeeping either, a plain field increment is correct and faster when only one thread will ever call through it. And immutability itself has a cost: copying a record on every `with` expression is more allocation than mutating a field in place, which matters on a hot path that is confined to a single thread and never crosses into concurrent territory at all. The right test in every one of these cases is the same one: does more than one thread touch this structure at an overlapping time. When the answer is no, the single-threaded version of a classic pattern is still the right one to write.

<blockquote class="ai-sidebar" markdown="1">
An assistant is a reasonable pair for making a classic pattern thread-safe once the decision to do so has already been made: it can draft the `ImmutableInterlocked.Update` call correctly on the first try, remember `[EnumeratorCancellation]` on an async iterator's token parameter, or suggest `Interlocked.Increment` for a counter a decorator keeps. What stays a human judgment is whether the code in front of it will run under concurrency at all, and which of these strategies fits the shape of the problem, an immutable snapshot, a channel, an interlocked counter, or nothing extra whatsoever. Asked to "make this observer thread-safe," an assistant will often add synchronization unconditionally, because adding a lock or an immutable collection looks like the safe answer regardless of whether the code it is touching will ever see two threads at once.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480/9780321700698) (Addison-Wesley, 1994): the source catalogue for Observer, Iterator, Decorator, and Command, the four patterns this chapter re-expresses for concurrent, asynchronous C#.
- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue this chapter draws on for naming the concurrency forces, safe publication, immutable sharing, and producer/consumer coordination, that these re-expressed patterns resolve.
- Microsoft Learn, [Iterators](https://learn.microsoft.com/en-us/dotnet/csharp/iterators) and [Generate and consume async streams](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream): the authority for the `IAsyncEnumerable<T>` state machine and the `[EnumeratorCancellation]` attribute `HandlingEventFeed` relies on.
- Microsoft Learn, [Immutable collections](https://learn.microsoft.com/en-us/dotnet/standard/collections/immutable): the reference for `ImmutableArray<T>` and `ImmutableInterlocked`, the primitives behind `HandlingEventHub`'s subscriber list.
- Microsoft Learn, [Handle and raise events](https://learn.microsoft.com/en-us/dotnet/standard/events/): the reference for the `event` keyword's own thread-safe add and remove accessors, the behavior this chapter's explicit `IHandlingObserver` interface makes raise-time safety more explicit than.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
