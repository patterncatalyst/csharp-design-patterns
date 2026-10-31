# 23-iterator-yield-linq-iasyncenumerable

The Iterator pattern in modern C#, shown as two halves of the same consignment
tracking domain: a synchronous buffered history and a live, cancellable
stream.

- **Synchronous `yield return`.** `ConsignmentHistory.Paginate` splits a
  consignment's `TrackingEvent`s into fixed-size `TrackingPage`s. The method
  itself is NOT an iterator: a C# iterator method defers its whole body,
  including argument checks, until the caller enumerates it, which would let
  an invalid page size slip past the call and only fail on the first
  `MoveNext`. `Paginate` validates `pageSize` eagerly and then returns the
  private `PaginateIterator`, which does the actual `yield return` work lazily
  a page at a time, including a trailing partial page only when events are
  left over, never an empty one when the count divides evenly.
- **LINQ over the same buffer.** `ConsignmentHistory.StagesReached` is a single
  `Select().Distinct()` query over the same events, standing next to the
  hand-written iterator to show both idioms solving related problems over the
  same backing sequence.
- **`IAsyncEnumerable<T>`.** `ICarrierFeed.StreamAsync` models a carrier's live
  tracking feed. `CarrierEventFeed` implements it as an `async IAsyncEnumerable<T>`
  iterator, checking the caller's `CancellationToken` and yielding one matching
  event at a time; the `[EnumeratorCancellation]` attribute on its token
  parameter is what lets a caller's `.WithCancellation(token)` reach that
  parameter instead of a fresh, uncancellable token. `LiveTrackingReader`
  consumes the stream with `await foreach`, either to completion
  (`CollectAsync`) or until a chosen stage is reached (`CollectUntilAsync`),
  which `break`s out of the loop right after adding the matching event so the
  feed is abandoned before anything past that stage is ever requested.

**What the tests prove.** `PaginationTests` checks a five-event history pages
into two full pages and a trailing partial one, a four-event history divides
evenly into two pages with no empty trailer, `Paginate(0)` throws
`ArgumentOutOfRangeException` on the call itself rather than on first
enumeration, and a spy `IReadOnlyList<TrackingEvent>` that counts its own
enumerations proves the iterator does no work until something actually walks
it. `StagesReached` is checked against a history with a repeated stage to
confirm it returns each stage once, in the order first seen.
`AsyncStreamTests` collects a full stream in order, confirms
`CollectUntilAsync` stops exactly at the requested stage, and feeds an
already-cancelled token through `StreamAsync` to confirm the stream throws
before yielding anything.

Shipping domain, no containers: both halves are pure, deterministic
collaborations over in-memory event sequences, with `Task.Yield()` standing in
for the network hop a real carrier call would make.

## Build and test

```sh
cd examples/23-iterator-yield-linq-iasyncenumerable
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
