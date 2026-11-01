# 35-monitor-object-and-active-object

Four coordination strategies wrapped around the same non-thread-safe stowage
servant, so the strategies can be compared on identical input.

- **The servant.** `StowagePlan` is plain, single-threaded stowage logic:
  `TryReserve` is a non-atomic read-modify-write over an internal dictionary,
  so handing it directly to concurrent callers can double-allocate a slot.
  Every stage below wraps this same servant rather than changing it.
- **Stage 1 - Monitor Object.** `SynchronizedStowagePlan` guards the servant
  with a single `System.Threading.Lock` gate. The reservation itself is
  synchronous, so the gate is held only as long as it takes to check and
  update the plan, and there is no `await` anywhere near the lock.
- **Stage 3 - an async-aware gate.** A reservation is not confirmed until its
  write lands in `IStowageLedger`, and that write is a genuine `await`. A
  plain `lock` statement cannot wrap an `await` (the compiler rejects it), so
  `SemaphoreStowagePlan` swaps the monitor for a `SemaphoreSlim` sized to one,
  which can be awaited across the ledger write while still serializing
  callers one at a time.
- **Stage 4 - Active Object on `Channel<T>`.** `StowageActiveObject` drops the
  gate entirely. Every reservation becomes a method request written to a
  bounded `System.Threading.Channels.Channel<T>`; a single background task
  drains the channel and is the only caller that ever touches the servant, so
  there is nothing left to synchronize. POSA2's Active Object participants
  map onto this type without any of them needing a name of their own: Proxy
  is `ReserveAsync`, Method Request is the private `ReserveRequest` record,
  Activation List is the bounded channel, Scheduler is the `await foreach`
  loop, Servant is `StowagePlan`, and Future is the `Task<T>` handed back
  through a `TaskCompletionSource<T>`. Writing to a full channel awaits, and
  that await is the backpressure: a caller is suspended until the scheduler
  drains room for one more request. `EnqueueAsync` is an internal test seam
  that splits "the write landed in the channel" from "the reservation
  finished," which is what lets a test observe a full channel deterministically.
- **Test doubles.** `InMemoryStowageLedger` stands in for durable I/O with a
  `Task.Yield()` so every append is a genuine asynchronous continuation.
  `GatedStowageLedger` signals the instant a write starts and then blocks
  until the test releases it, pinning the scheduler at a known point.
  `ThrowOnceStowageLedger` fails its first write and succeeds after that.

**Why Stage 2 is absent.** The natural next step from Stage 1 is putting the
ledger write inside the same `lock` statement the monitor already holds. That
does not compile: C# rejects an `await` inside a `lock` body. This example
ships no code for that stage because there is no code to ship; the chapter
covers the compile error itself.

**What the tests prove.** For the Monitor Object, a `Barrier` releases 16
`Task.Run` readers as close to simultaneously as the scheduler allows against
`SynchronizedStowagePlan`, and the test asserts every returned slot is
distinct and the plan's own count matches the number of successful
reservations. A second test runs the same load against the bare servant
sequentially, with no concurrency, to show the invariant holds without a
coordination strategy when there is no race to begin with. For the semaphore
gate, 500 concurrent `ReserveAsync` calls across the async ledger write are
asserted distinct the same way, and a pre-cancelled token is asserted to
throw before any reservation happens. For the Active Object, 1,000 concurrent
`ReserveAsync` calls against a capacity-64 channel are asserted distinct;
pinning the scheduler on a gated ledger lets one test prove a capacity-1
channel's third write is still pending (`!pendingWrite.IsCompleted`) while the
channel is full, then drains once the gate releases; another proves a queued
request's own cancellation token is honoured before its turn comes; and a
`ThrowOnceStowageLedger` proves one failed reservation surfaces its exception
to the caller that made it while the scheduler keeps serving every
reservation after it.

No `Thread.Sleep`, no `Task.Delay`, and no wall-clock reads anywhere in this
example, in the library or the tests: the ledger uses `Task.Yield()` for its
asynchronous continuation, concurrent tests use `Task.WhenAll` or a `Barrier`
to force contention, and the backpressure test asserts on
`!pendingWrite.IsCompleted` against a channel of known capacity instead of
racing a timeout.

## Build and test

```sh
cd examples/35-monitor-object-and-active-object
dotnet test --solution StowageCoordination.slnx
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
