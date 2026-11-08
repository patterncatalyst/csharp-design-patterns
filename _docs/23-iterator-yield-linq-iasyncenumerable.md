---
title: "Iterator: yield return, LINQ, and IAsyncEnumerable"
order: 23
layout: tutorial
sectionid: docs
part: "coordinating-traversing"
description: "The pattern as a compiler-generated state machine, in its synchronous and asynchronous forms."
---

<!-- EXAMPLE (shipping domain): Paging a tracking history; IAsyncEnumerable over a carrier's live event feed. -->

## Learning objectives

By the end of this chapter you can:

- Explain why a `yield return` method defers its whole body, argument checks included, and split eager validation out of a private iterator as `ConsignmentHistory.Paginate` does.
- Describe the state machine the compiler generates for an iterator method, resumed in `MoveNext` where the last `yield return` left off.
- Read `StagesReached` as composed iterators, `Select` then `Distinct`, that touch no event until enumerated.
- Write an `async` iterator returning `IAsyncEnumerable<T>`, mark its token `[EnumeratorCancellation]`, and connect it to a consumer's `.WithCancellation(token)`.
- Trace what an early `break` out of `await foreach` does to the producer.

## Why this matters

The 1994 Iterator assumes the whole mechanism is hand-written: a separate iterator class with a `First`/`Next`/`IsDone`/`CurrentItem` protocol, fields tracking position, and a collection that hands out a fresh iterator instead of exposing its storage. Every container needs its own version, and getting it right means getting the state machine right, including two independent iterators over the same collection at once.

C# doesn't ask for that class. A method containing `yield return` reads as an ordinary method returning a sequence, and the compiler generates the iterator underneath: the state machine, the position tracking, and the `IEnumerator<T>` and `IEnumerable<T>` implementations. LINQ's operators are iterators built the same way, composed into pipelines that don't run until something pulls from the end. And an `async` method returning `IAsyncEnumerable<T>` can `await` between each `yield return`, giving a producer that waits on real asynchronous work the same lazy, pull-based shape.

{% include excalidraw.html name="23-iterator-yield-linq-iasyncenumerable" caption="Figure 23.1: Paginate validates eagerly and throws immediately on bad input, then delegates to PaginateIterator, whose yield return body the compiler turns into a state machine that produces one page at a time only once a caller enumerates it." %}

## Pattern: Iterator

### The problem

A consignment's tracking history is read a page at a time, as a list of distinct stages, and, for a live shipment, from a carrier feed whose events arrive over time. Callers want the elements in order without knowing how the history stores them, and without paying for elements they never read.

### The solution

An **Iterator** provides sequential access to the elements of a collection without exposing how the collection is built.

### How C# models it

`ConsignmentHistory` wraps a fully buffered sequence of `TrackingEvent` records:

```csharp
public sealed class ConsignmentHistory
{
    private readonly IReadOnlyList<TrackingEvent> _events;

    public ConsignmentHistory(IEnumerable<TrackingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _events = events as IReadOnlyList<TrackingEvent> ?? [.. events];
    }
}
```

The constructor copies only when the argument isn't already an `IReadOnlyList<TrackingEvent>`. A list passed in is stored as-is and isn't enumerated until something walks the history.

### Eager validation, lazy body

The public method contains no `yield return`; the private one does:

```csharp
public IEnumerable<TrackingPage> Paginate(int pageSize)
{
    if (pageSize <= 0)
    {
        throw new ArgumentOutOfRangeException(
            nameof(pageSize), pageSize, "Page size must be a positive number of events.");
    }

    return PaginateIterator(pageSize);
}

private IEnumerable<TrackingPage> PaginateIterator(int pageSize)
{
    var pageNumber = 1;
    var buffer = new List<TrackingEvent>(pageSize);

    foreach (var trackingEvent in _events)
    {
        buffer.Add(trackingEvent);
        if (buffer.Count == pageSize)
        {
            yield return new TrackingPage(pageNumber, buffer);
            pageNumber++;
            buffer = new List<TrackingEvent>(pageSize);
        }
    }

    if (buffer.Count > 0)
    {
        yield return new TrackingPage(pageNumber, buffer);
    }
}
```

`PaginateIterator` holds the state machine: `pageNumber`, `buffer`, and the position in the loop over `_events`, all captured and resumed by the generated `MoveNext` each time a `foreach` asks for the next page. Calling `PaginateIterator(2)` runs none of the body. It returns an `IEnumerable<TrackingPage>` whose `MoveNext` hasn't been called.

That's why `Paginate` can't contain the `yield return` itself. A method with `yield return` anywhere defers its whole body, so an `ArgumentOutOfRangeException` at the top would fire only on first enumeration, far from the call that passed the bad `pageSize`. Keeping the check in a method with no `yield return` makes it eager.

| Call | Effect |
| --- | --- |
| `history.Paginate(0)`, never enumerated | Throws `ArgumentOutOfRangeException` at the call |
| `history.Paginate(2)`, held but not enumerated | The source's enumerator count stays at 0 |
| `.ToList()` on that result | The source is enumerated once |
| Five events, page size 2 | Three pages: two full, one with the leftover event |
| Four events, page size 2 | Two full pages; the `if (buffer.Count > 0)` guard adds no empty page |

### LINQ operators as composed iterators

`StagesReached` returns the distinct stages in the order each was first seen:

```csharp
public IEnumerable<TrackingStage> StagesReached() => _events.Select(e => e.Stage).Distinct();
```

`Select` and `Distinct` are iterator methods with the same deferred shape. Composing them builds a chain of iterators, each wrapping the one before, that produces nothing until the outermost is enumerated. `Select` projects each event to its `Stage` without an intermediate list; `Distinct` keeps a hidden set, yields a stage the first time it appears and skips repeats, in one pass. A history with `PickedUp` early and again later returns `PickedUp` once, in its first position.

The method has no loop, no buffer and no visible state. The walking is left to the operators.

### IAsyncEnumerable and await foreach

A buffered history knows every event it will report. A live carrier feed doesn't: events arrive over time from a source a consumer can't run ahead of.

```csharp
public interface ICarrierFeed
{
    IAsyncEnumerable<TrackingEvent> StreamAsync(string consignmentId, CancellationToken cancellationToken);
}
```

`CarrierEventFeed` implements it over an in-memory sequence standing in for a carrier API:

```csharp
public async IAsyncEnumerable<TrackingEvent> StreamAsync(
    string consignmentId, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    foreach (var trackingEvent in _events)
    {
        if (trackingEvent.ConsignmentId != consignmentId)
        {
            continue;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Stand in for the network hop an actual carrier call would make,
        // without any wall-clock delay that would make the test suite flaky.
        await Task.Yield();

        yield return trackingEvent;
    }
}
```

`async` and the `await Task.Yield()` make each `yield return` asynchronous: it can sit behind real work, here a stand-in for the network hop, without blocking the waiting thread. A consumer walks it with `await foreach`, awaiting each event.

An async iterator's parameters are captured once, when the enumerable is created. Without help, a plain `await foreach` would leave `cancellationToken` bound to a default, uncancellable token. `[EnumeratorCancellation]` tells the compiler to substitute whatever token the consumer passes to `.WithCancellation(token)`, so `ThrowIfCancellationRequested()` sees a cancellation requested after the stream was created.

The consumer runs the feed to completion:

```csharp
public async Task<IReadOnlyList<TrackingEvent>> CollectAsync(
    ICarrierFeed feed, string consignmentId, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(feed);

    var collected = new List<TrackingEvent>();
    await foreach (var trackingEvent in feed.StreamAsync(consignmentId, cancellationToken)
        .WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        collected.Add(trackingEvent);
    }

    return collected;
}
```

`.WithCancellation(cancellationToken)` reaches the marked parameter. `.ConfigureAwait(false)` tells the runtime not to resume on a captured context after each step, the usual rule for library code with no reason to assume a hosting environment. Four events for one consignment come back in the order the feed holds them. A token cancelled before `CollectAsync` starts throws `OperationCanceledException` on the first iteration, before any event is produced.

### Stopping early

`CollectUntilAsync` stops at a given stage:

```csharp
public async Task<IReadOnlyList<TrackingEvent>> CollectUntilAsync(
    ICarrierFeed feed, string consignmentId, TrackingStage stop, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(feed);

    var collected = new List<TrackingEvent>();
    await foreach (var trackingEvent in feed.StreamAsync(consignmentId, cancellationToken)
        .WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        collected.Add(trackingEvent);
        if (trackingEvent.Stage == stop)
        {
            // Abandon the feed early: the enumerator is disposed once the
            // await foreach is left without exhausting it, so no event
            // past the stop stage is ever requested.
            break;
        }
    }

    return collected;
}
```

Leaving an `await foreach` early disposes the `IAsyncEnumerator<T>`, so `StreamAsync`'s own loop never reaches the events after the match; the producer stops instead of running on in the background. With six events and a stop stage of `InTransit`, the third, the result holds three events ending on `InTransit`, and the last three are never produced.

{% include excalidraw.html name="23-async-stream-await-foreach" caption="Figure 23.2: CarrierEventFeed.StreamAsync marks its cancellation token with [EnumeratorCancellation] so a consumer's WithCancellation call reaches it. CollectUntilAsync breaks early on a matching stage, disposing the enumerator before later events are ever requested." %}

### Deferred execution end to end

{% include excalidraw.html name="23-deferred-execution" caption="Figure 23.3: Paginate's validation is the one eager step; everything after it is deferred. PaginateIterator and the StagesReached LINQ query both sit inert until a foreach pulls the first element through them." %}

The `pageSize <= 0` check is the one eager step: it runs when `Paginate` is called and fails synchronously at the call site. Everything after it is lazy. `PaginateIterator` and the `Select`-then-`Distinct` chain both sit inert until a `foreach` or `.ToList()` calls `MoveNext`, so a caller holding an unenumerated sequence has paid for none of its work.

Nothing is cached between enumerations. Calling `Paginate(2)` and enumerating the result twice runs `PaginateIterator`'s loop twice. Materialize with `.ToList()` when a result is read more than once or handed to code that expects a fixed collection.

## When not to use it

- **Lazy sequences for small, fully read data.** Laziness pays when a caller may not need every element, when the full result is expensive or impossible to build up front, or when elements arrive over time. A three-event history read in full gains nothing from a state machine and a one-pass walk; return `List<TrackingPage>`.
- **Re-enumerating a sequence with side effects.** A `yield return` body re-runs from the start on every enumeration. Harmless for a pure projection like `StagesReached`, wrong for a body that increments a counter, logs or calls a service per element. A caller that reads twice, or needs the same answer both times, should materialize once with `.ToList()` or `.ToArray()`.
- **`IAsyncEnumerable<T>` over in-memory data.** Wrapping data already in memory adds a state machine and an `await` per element for nothing. `CarrierEventFeed` uses it because a live feed is an asynchronous source over time; a method over in-memory data stays synchronous.

> **🤖 Explore with your agent**
>
> "In `ConsignmentHistory`, make `Paginate(int pageSize)` a plain method that throws `ArgumentOutOfRangeException` for a size below 1 and returns `PaginateIterator(pageSize)`, which holds the `yield return` and emits `TrackingPage` records with no trailing empty page. Leave `StagesReached` a lazy `Select` and `Distinct` with no `ToList`. Use `IAsyncEnumerable<TrackingEvent>` only for `ICarrierFeed.StreamAsync`, mark its token `[EnumeratorCancellation]`, and let `LiveTrackingReader.CollectUntilAsync` `break` out of `await foreach` at the stop stage."
>
> Run `dotnet test --project TrackingHistory.Tests --filter PaginationTests`; `Paginate_validates_page_size_eagerly_before_enumeration` fails if the check moves into the iterator.

## Build, run, observe

```sh
cd examples/23-iterator-yield-linq-iasyncenumerable
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. The async half runs on `Task.Yield()`, not a wall-clock delay, so nothing waits on real time. All 8 tests pass: five for the synchronous iterator (paging, the trailing page, eager validation, deferral, distinct stages) and three for the async stream (ordered delivery, early stop, cancellation).

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Iterator, written against a hand-rolled `First`/`Next`/`IsDone`/`CurrentItem` protocol.
- Microsoft Learn, [Iterators (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/iterators): `yield return`, the compiler-generated state machine, and deferred execution.
- Microsoft Learn, [Generate and consume async streams with C# and .NET](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream): `IAsyncEnumerable<T>`, `await foreach`, `[EnumeratorCancellation]` and `.WithCancellation(...)`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
