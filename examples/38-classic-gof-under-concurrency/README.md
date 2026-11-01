# 38-classic-gof-under-concurrency

Five classic GoF patterns, each re-examined for what changes when the
participants are shared across threads instead of owned by one call stack.

- **Observer, without the `event` keyword.** `HandlingEventHub` publishes
  `HandlingEvent` instances to every subscribed `IHandlingObserver`. A
  multicast delegate field is not the receiver here: subscribers live in an
  `ImmutableArray<IHandlingObserver>`, swapped in with
  `ImmutableInterlocked.Update`, so a subscribe or unsubscribe never mutates
  the array a concurrent `Raise` is iterating. `Raise` captures one reference
  to the current array before looping, and that snapshot cannot change
  underneath the loop no matter what another thread does to the field
  afterward.
- **Iterator as `IAsyncEnumerable<T>`.** `HandlingEventFeed` wraps an unbounded
  `System.Threading.Channels.Channel<T>`. `StreamAsync` is an `await foreach`
  over `ChannelReader<T>.ReadAllAsync`, so a consumer can iterate a stream that
  is still being written: it suspends between items instead of reaching the
  end of a fixed collection, and resumes the instant the next event is
  published or the channel is completed.
- **Decorator over async.** `CountingValidatorDecorator` wraps an
  `IManifestValidator` and implements the same interface, forwarding every
  call to the inner `ManifestValidator` and counting it with
  `Interlocked.Increment` so the count stays accurate when many callers are
  awaiting `ValidateAsync` concurrently.
- **Command as `Task<T>`.** `RecordHandlingCommand` packages one handling
  event and its ledger as an `IHandlingCommand`, returning a `Task<HandlingReceipt>`
  instead of calling `void Execute()`. `CommandInvoker.RunAllAsync` starts a
  whole batch of commands at once and gathers every result with
  `Task.WhenAll`, rather than running them one at a time.
- **Immutability as the strategy underneath all of them.** `TrackingId`,
  `HandlingEvent`, and `HandlingReceipt` are a readonly record struct and
  sealed records. None of the patterns above need a lock to guard the data
  they pass around, because that data cannot change after construction; a
  caller that wants a different value produces an independent copy with a
  `with` expression instead of mutating the shared instance.
- **Test doubles.** `CountingObserver` records how many times it was notified
  and every event it saw, safely under concurrent raises. `UnsubscribingObserver`
  disposes another observer's subscription the instant it is itself notified,
  which is what proves a raise in progress is unaffected by an unsubscribe
  that happens during it.

**What the tests prove.** For Observer, one test raises a sequence of events
against several subscribers and checks each one saw every event; a second
test has many threads race to raise distinct events through a `Barrier` and
checks the shared observer's count and recorded set match what was produced,
with no duplicate or dropped delivery; a third subscribes two observers where
the first disposes the second's subscription on notification, and shows the
first raise still reaches both (the in-flight snapshot was fixed before the
unsubscribe happened) while the second raise reaches only the one still
subscribed. For Iterator, one test publishes a known sequence and checks the
stream yields it back in order; a second passes an already-cancelled token
and asserts the stream throws before yielding anything; a third runs a
producer and a consumer concurrently over the same feed and checks every
published event was consumed. For Decorator, many concurrent calls through
the counting wrapper are asserted to all return `true` and to leave the call
counter exactly equal to the number of calls. For Command, one test checks a
single command's receipt carries a sequence number and that the event landed
in the ledger; a second runs a batch of commands through the invoker and
checks the returned sequence numbers are exactly `1..N` with no duplicates or
gaps. For immutability, many threads read every field of one shared event
concurrently and each produces its own `with`-copy changing only the
sequence; the test asserts the original is untouched afterward and that each
copy is value-equal to the original except for its own sequence number.

No `Thread.Sleep`, no `Task.Delay`, and no wall-clock reads anywhere in this
example, in the library or the tests: asynchronous "I/O" uses `Task.Yield()`
for its continuation, concurrent tests use `Task.WhenAll` or a `Barrier` to
force contention deterministically, and the async stream is drained with
`Complete()` plus `await foreach` against a known event count rather than a
timeout.

## Build and test

```sh
cd examples/38-classic-gof-under-concurrency
dotnet test --solution ConcurrentHandling.slnx
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
