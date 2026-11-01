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

- Explain why a `yield return` method defers its entire body, including
  argument validation, until something enumerates it, and why
  `ConsignmentHistory.Paginate` splits eager validation out of a private
  `PaginateIterator` method to avoid that trap.
- Describe what the compiler generates for an iterator method: a state
  machine implementing `IEnumerator<T>`, resumed inside `MoveNext` from
  wherever the previous `yield return` left off.
- Read `StagesReached` as a composed LINQ query, `Select` followed by
  `Distinct`, and explain why neither operator touches a single
  `TrackingEvent` until a caller enumerates the result.
- Build an asynchronous producer with `async` and `yield return` against
  `IAsyncEnumerable<T>`, attach `[EnumeratorCancellation]` to its token
  parameter, and explain what a consumer's `.WithCancellation(token)` call
  wires up on the other end of that attribute.
- Trace what happens when `LiveTrackingReader.CollectUntilAsync` breaks out
  of an `await foreach` early, including why `CarrierEventFeed` never
  produces the events that come after the stop stage.

## Why this matters

The 1994 statement of Iterator assumes a programmer writes the whole
mechanism by hand: a separate iterator class, a `First`/`Next`/`IsDone`/
`CurrentItem` protocol, private fields tracking a current position and
whatever bookkeeping the traversal needs, and a collection class that hands
out a fresh iterator object on request rather than exposing its internal
storage directly. Every container that wants to be walked one element at a
time needs its own version of that class, written again for each new shape
of data, because the pattern is a design to follow, not a feature a language
provides. Getting it right means getting the state machine right: what
`Next` does to move forward, what `IsDone` checks to know when to stop, and
what happens if two callers ask for independent iterators over the same
collection at once.

C# 14 and .NET 10 do not ask a developer to write that class. A method with
a `yield return` statement in its body is, as far as the person writing it
is concerned, an ordinary method that returns a sequence. The compiler reads
that `yield return` and generates the iterator class underneath it: the
state machine, the position tracking, the implementation of `IEnumerator<T>`
and `IEnumerable<T>`, all synthesized from a method body that reads like
straight-line code with a `foreach` loop and a `yield return` sitting inside
it. `ConsignmentHistory.Paginate` in this chapter's example never implements
an interface by hand; it calls a private method whose body is nothing more
than a loop and two `yield return` statements, and the pagination machinery
that a 1994 reading of this pattern would spell out as a dedicated class
exists only in the IL the compiler emits.

The same mechanism extends past hand-written loops. LINQ's sequence
operators, `Select`, `Where`, `Distinct`, and the rest, are themselves
iterators, built the same way, composed into a pipeline that does not run
until something pulls from the far end. `ConsignmentHistory.StagesReached`
chains two of them together without writing a single `yield return` of its
own, because the operators already carry that machinery. More recently, the
same `yield return` syntax extends to asynchronous production: an `async`
method returning `IAsyncEnumerable<T>` can `await` work between each `yield
return`, giving a producer that waits on real asynchronous operations the
same lazy, pull-based shape a synchronous iterator has. `CarrierEventFeed`
in this chapter's example is exactly that: an asynchronous stream of
tracking events that a caller walks with `await foreach`, one event at a
time, without the caller or the producer needing to buffer the whole feed in
memory first. The 1994 pattern names the problem, traversing a collection's
elements without exposing its internal representation. Modern C# answers it
with a compiler feature that turns a plain-looking method into that
traversal mechanism, synchronously or asynchronously, without a hand-written
iterator class anywhere in sight.

{% include excalidraw.html name="23-iterator-yield-linq-iasyncenumerable" caption="Figure 23.1: Paginate validates eagerly and throws immediately on bad input, then delegates to PaginateIterator, whose yield return body the compiler turns into a state machine that produces one page at a time only once a caller enumerates it." %}

## yield return and the state machine it becomes

The example behind this chapter lives in
`examples/23-iterator-yield-linq-iasyncenumerable`. `ConsignmentHistory`
wraps an already-known, fully buffered sequence of `TrackingEvent` records
for one consignment:

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

The constructor accepts whatever `IEnumerable<TrackingEvent>` a caller
passes, but it only copies into a fresh list when the argument is not
already an `IReadOnlyList<TrackingEvent>`. That distinction matters for one
of the tests described later in this chapter: a test double that implements
`IReadOnlyList<TrackingEvent>` directly gets stored as-is, which means it is
not enumerated at construction time at all, only later, when something
walks the history it produces.

`Paginate` is the method a caller actually reaches for, and it is
not the method that contains the `yield return` statements:

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

`PaginateIterator` is where the state machine lives: a `pageNumber` counter,
a `buffer` list, and a position inside the loop over `_events`, all of it
captured and resumed by the compiler-generated `MoveNext` every time a
caller's `foreach` asks for the next element. None of that state is visible
in the method's own signature; it exists only because the method body
contains `yield return`, which is the one syntactic signal that tells the
compiler to synthesize a class instead of compiling the method body as
written. Calling `PaginateIterator(2)` does not run a single line of that
body. It returns an object implementing `IEnumerable<TrackingPage>` whose
`MoveNext` has not been called yet, and the loop, the buffer, and every
intermediate `TrackingPage` only come into existence once something starts
pulling elements from that object.

That deferral is exactly why `Paginate` cannot afford to contain the
`yield return` statements itself. A method with `yield return` anywhere in
its body defers its entire body, not just the loop, which means an
`ArgumentOutOfRangeException` thrown at the top of that same method would
not throw when `Paginate` is called; it would throw only when a caller's
`foreach` or `.ToList()` first enumerates the result, far from the call site
that passed the bad `pageSize` in. `ConsignmentHistory` avoids that by
keeping `Paginate` itself free of `yield return`: it validates `pageSize`
as an ordinary, eager check, and only after that check passes does it call
`PaginateIterator`, handing back the lazy sequence that method returns.
`PaginationTests.Paginate_validates_page_size_eagerly_before_enumeration`
calls `history.Paginate(0)` directly, with no `foreach` or `.ToList()`
anywhere near it, and asserts `Assert.Throws<ArgumentOutOfRangeException>`
around that bare call, which only passes because `Paginate` throws before
`PaginateIterator` is ever reached.

The companion test, `Paginate_is_deferred_until_enumerated`, proves the
other half of the same split. It wraps a `CountingEventSource`, a private
test type implementing `IReadOnlyList<TrackingEvent>` that increments an
`EnumerationCount` field every time its `GetEnumerator` runs, and passes
that into `ConsignmentHistory`. Calling `history.Paginate(2)` and holding
the returned `IEnumerable<TrackingPage>` leaves `spy.EnumerationCount` at
zero: nothing inside `_events` has been touched yet, because
`PaginateIterator`'s body, including its own `foreach (var trackingEvent in
_events)` loop, has not started running. Only after the test calls
`pages.ToList()`, forcing a full enumeration, does `EnumerationCount`
become `1`. `Paginate_yields_fixed_size_pages_and_a_final_partial_page`
and `Paginate_with_an_evenly_dividing_size_has_no_trailing_empty_page`
both confirm the buffering logic itself: five events paginated at size two
produce three pages, two full and one holding the single leftover event,
while four events paginated at size two produce exactly two full pages with
no trailing empty one, because the `if (buffer.Count > 0)` guard after the
loop only emits a final page when something is left in the buffer.

## LINQ operators as iterators that compose

`StagesReached` answers a different question than `Paginate`: not how to
split a history into pages, but which distinct stages a consignment has
reached, in the order each one was first seen.

```csharp
public IEnumerable<TrackingStage> StagesReached() => _events.Select(e => e.Stage).Distinct();
```

Neither `Select` nor `Distinct` is a hand-written loop. Both are themselves
iterator methods, implemented with the same deferred, state-machine shape
`PaginateIterator` demonstrates directly, and composing them does not run
either one; it builds a chain of iterators, each wrapping the one before it,
that only starts producing values once something enumerates the outermost
one. `Select` projects each `TrackingEvent` to its `Stage` without
materializing an intermediate list of stages, and `Distinct` tracks which
stages it has already returned in a hidden set, yielding a stage the first
time it sees it and skipping every repeat after that, all without a second
pass over the data. `PaginationTests.StagesReached_returns_distinct_stages_in_first_seen_order`
runs a history where `PickedUp` appears twice, once early and once later,
and asserts the result keeps `PickedUp` in its first position and drops the
second occurrence entirely, which is `Distinct`'s guarantee: first
occurrence wins, order preserved, no stage repeated.

That `StagesReached` reads as a single expression, with no loop, no buffer,
and no explicit state anywhere in its own body, is the same dissolve that
`PaginateIterator` demonstrates at a lower level, one step further up: once
`yield return` exists as a language feature, the sequence operators built on
top of it stop looking like code that walks a collection and start looking
like a description of what the result should contain, with the walking left
entirely to the operators underneath.

## Deferred execution end to end

`Paginate` and `StagesReached` sit on either side of the same eager and
deferred split, and seeing them side by side is the clearest way to read
what "deferred" buys a caller.

{% include excalidraw.html name="23-deferred-execution" caption="Figure 23.3: Paginate's validation is the one eager step; everything after it is deferred. PaginateIterator and the StagesReached LINQ query both sit inert until a foreach pulls the first element through them." %}

`Paginate(pageSize)` is the one eager checkpoint in this example: the
`pageSize <= 0` check runs the moment the method is called, before the
method returns anything at all, and its failure mode is an exception thrown
synchronously at the call site. Everything downstream of that checkpoint is
lazy. `PaginateIterator`'s body sits completely inert until a `foreach`
or an operator like `.ToList()` calls `MoveNext` on it for the first time,
and `StagesReached`'s `Select`-then-`Distinct` chain is inert in the same
way: calling the method builds the chain of iterator objects, but no
`TrackingEvent` is read, no stage is projected, and no duplicate is filtered
until a consumer starts pulling values through it. A caller holding either
sequence without enumerating it has not paid for any of the work that
sequence represents. That property is also why a page count or a distinct
stage list should not be assumed to exist, correct or otherwise, until it is
walked. Calling `Paginate(2)` twice on the same `ConsignmentHistory`
and enumerating each result separately re-runs `PaginateIterator`'s loop
from the start both times; nothing is cached between enumerations unless a
caller materializes the sequence into a list first, which is
exactly what `.ToList()` does when a result needs to be read more than once
or handed somewhere that expects a fixed collection rather than a sequence
that can only be walked once at a time.

## IAsyncEnumerable and await foreach over a live feed

A buffered `ConsignmentHistory` already knows every event it will ever
report. A carrier's live tracking feed does not: new events arrive over
time, from a source a consumer cannot run ahead of, and the shape that fits
that situation is an asynchronous stream rather than a synchronous one.
`ICarrierFeed` is the interface this chapter's example builds around:

```csharp
public interface ICarrierFeed
{
    IAsyncEnumerable<TrackingEvent> StreamAsync(string consignmentId, CancellationToken cancellationToken);
}
```

`CarrierEventFeed` implements it over an in-memory sequence standing in for
a real carrier API, and its `StreamAsync` method is the asynchronous
counterpart to `PaginateIterator`: an `async` method with `yield return`
statements in its body, returning `IAsyncEnumerable<TrackingEvent>` instead
of `IEnumerable<TrackingEvent>`.

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

The `async` modifier and the `await Task.Yield()` call are what make this
method's `yield return` statements asynchronous rather than synchronous:
each one can sit behind real asynchronous work, here a stand-in for the
network round trip a live carrier integration would make, without
blocking the thread that is waiting on it. A consumer walks this with
`await foreach` instead of a plain `foreach`, and each iteration of that
loop awaits the next event becoming available rather than spinning
synchronously until one is ready.

The `[EnumeratorCancellation]` attribute on the `cancellationToken`
parameter solves a problem specific to this shape. An `async
IAsyncEnumerable<T>` method's parameters are captured once, when the
enumerable is first created, not on every call to `MoveNextAsync`, and a
plain `await foreach` without `.WithCancellation(...)` would otherwise leave
this method's `cancellationToken` parameter bound to a default,
uncancellable token the compiler supplies on its own. `[EnumeratorCancellation]`
tells the compiler that this particular parameter should instead be
replaced by whatever token a consumer passes to `.WithCancellation(token)`
on the returned `IAsyncEnumerable<T>`, which is what lets `cancellationToken
.ThrowIfCancellationRequested()` inside `StreamAsync` observe a
cancellation a consumer requested after the stream was already created.

`LiveTrackingReader` is the consumer side of that contract, and both of its
methods follow the same `await foreach` shape with one difference in how
they end:

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

`.WithCancellation(cancellationToken)` is the call that reaches the
`[EnumeratorCancellation]`-marked parameter on `StreamAsync`, and
`.ConfigureAwait(false)` is the same library-code discipline that applies to
any other `await` in code that does not own a synchronization context: it
tells the runtime not to bother resuming back on a captured context after
each awaited step, which matters for a library like this one with no reason
to assume a particular hosting environment on the other side of the call.
`CollectAsync` walks the feed to completion, appending every event it sees.
`CollectUntilAsync` walks the same feed but stops early:

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

The `break` inside this `await foreach` does more than exit a loop. Leaving
an `await foreach` before it completes disposes the underlying
`IAsyncEnumerator<T>`, which means `StreamAsync`'s own `foreach` over
`_events` never reaches the iterations past the matching one; the producer
side stops, rather than continuing to run in the background while
the consumer ignores the rest. `AsyncStreamTests.CollectUntilAsync_stops_the_feed_early_on_break`
runs six events through `CollectUntilAsync` with a stop stage of
`TrackingStage.InTransit`, which is the third event in that sequence, and
asserts the collected list has exactly three entries ending on
`InTransit`, proving the three events after the stop stage were never
produced at all, not merely produced and discarded.

{% include excalidraw.html name="23-async-stream-await-foreach" caption="Figure 23.2: CarrierEventFeed.StreamAsync marks its cancellation token with [EnumeratorCancellation] so a consumer's WithCancellation call reaches it. CollectUntilAsync breaks early on a matching stage, disposing the enumerator before later events are ever requested." %}

`AsyncStreamTests.StreamAsync_yields_every_event_in_order` is the baseline
case: four events for one consignment, collected through `CollectAsync`,
come back in the exact order `CarrierEventFeed` holds them in, confirming
the stream neither reorders nor drops anything when it runs to completion.
`StreamAsync_honours_cancellation` cancels a `CancellationTokenSource`
before `CollectAsync` even starts, then asserts the call throws some
`OperationCanceledException`, which is the `cancellationToken
.ThrowIfCancellationRequested()` call inside `StreamAsync` firing on the
very first iteration because the token it was handed, through
`[EnumeratorCancellation]`, was already in a cancelled state before a single
event was produced.

## Build, run, observe

The synchronous half of this example needs nothing beyond the local .NET 10
SDK, and the asynchronous half runs on `Task.Yield()` rather than any
wall-clock delay, so the whole suite executes without waiting on real time.
The example is standalone-testable from its own directory:

```sh
cd examples/23-iterator-yield-linq-iasyncenumerable
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 8
  failed: 0
  succeeded: 8
  skipped: 0
  duration: 839ms
```

The eight cases split across what this chapter covers. `PaginationTests`
proves the synchronous iterator half: fixed-size paging with a trailing
partial page, an evenly dividing page size with no trailing empty page,
eager validation of `pageSize` that throws before any enumeration happens,
deferred execution confirmed through a `CountingEventSource` that only
reports an enumeration count after `.ToList()` runs, and `StagesReached`
returning distinct stages in first-seen order. `AsyncStreamTests` proves the
asynchronous stream half: every event arriving through `StreamAsync` in
order, `CollectUntilAsync` stopping the feed early on a matching stage with
every later event never produced, and an already-cancelled token stopping
the stream before it yields a single event.

## When not to use it

A lazy, pull-based sequence is worth reaching for when a caller might not need
every element, when the full sequence would be expensive or impossible to
materialize up front, or when production happens over time, as it
does for a live carrier feed. None of those conditions hold for a short,
already-known list that a caller is always going to read in full. A
`ConsignmentHistory` with three events gains nothing from paginating lazily
instead of just returning `List<TrackingPage>` directly; the deferred
machinery, the state machine, the one-pass-only walk, is overhead a reader
has to reason about for a collection small enough that eagerly building the
whole result costs nothing worth avoiding.

Re-enumeration is the sharper trap. An `IEnumerable<T>` backed by a `yield
return` method re-runs its entire body from the start every time something
enumerates it again, which is harmless for a pure projection like
`StagesReached` but wrong for a sequence with a side effect in its
body: an iterator method that increments a counter, writes a log line, or
calls out to another service once per element will do all of that again,
in full, on a second `foreach` over the same result. A caller that needs to
read a sequence more than once, or that needs the guarantee that walking it
twice produces the same answer regardless of what changed in between,
should materialize it once with `.ToList()` or `.ToArray()` and pass the
materialized collection around instead of passing the lazy sequence itself.

`IAsyncEnumerable<T>` carries its own cost on top of that. Wrapping a
source that is already fully available in memory behind an asynchronous
stream adds state-machine overhead and an `await` on every element for no
benefit, when a plain, synchronous `IEnumerable<T>` or a pre-materialized
list would answer the same question with less machinery. `CarrierEventFeed`
earns `IAsyncEnumerable<TrackingEvent>` because a live carrier feed is
exactly the case where elements arrive from an asynchronous
source over time; a method standing over data that is already sitting in
memory should stay synchronous rather than reaching for the asynchronous
form on the assumption that it looks more thorough.

<blockquote class="ai-sidebar" markdown="1">
Asked to "stream" or "page" a result, an AI pairing
partner will sometimes reach for `IAsyncEnumerable<T>` and `await foreach`
even when every element the method produces is already sitting in memory
and the whole thing could be returned as a `List<T>` with no asynchronous
work anywhere inside it, treating the asynchronous stream shape as a
default for any method that produces more than one value rather than a
shape reserved for sources that produce elements over time. A
related suggestion wraps a `yield return` method's validation inside the
iterator itself, with an argument check at the top of a method body that
also contains `yield return` further down, which defers that check until
first enumeration instead of throwing it at the call site, the exact trap
`ConsignmentHistory.Paginate` avoids by keeping its own body free of
`yield return` and delegating the lazy part to `PaginateIterator`. A
generated answer can also call `.ToList()` immediately after building a
LINQ query "to be safe," collapsing a lazy pipeline like
`StagesReached` into an eagerly materialized one for no reason the request
asked for. Check a generated answer against what this chapter
built: reach for `IAsyncEnumerable<T>` when production is
asynchronous and ongoing, keep eager checks out of any method whose body
contains `yield return`, and leave a LINQ query lazy unless something
about the caller's use of it requires a materialized list.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Iterator, written against a hand-rolled `First`/`Next`/`IsDone`/`CurrentItem` protocol this chapter's compiler-generated state machine replaces.
- Microsoft Learn, [Iterators (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/iterators): the authority behind `yield return`, the compiler-generated state machine, and the eager-versus-deferred split `ConsignmentHistory.Paginate` and `PaginateIterator` demonstrate.
- Microsoft Learn, [Generate and consume async streams with C# and .NET](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream): the authority behind `IAsyncEnumerable<T>`, `await foreach`, `[EnumeratorCancellation]`, and `.WithCancellation(...)`, all of which `CarrierEventFeed` and `LiveTrackingReader` build on directly.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
