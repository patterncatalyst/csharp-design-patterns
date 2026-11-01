# 33-why-dotnet-chose-stackless-async

Why .NET compiles an `async` method into a stackless state machine instead of
giving each one a dedicated stack, demonstrated with a sailing booking desk.

- **`IBookingLedger`.** `ConfirmAsync` confirms a consignment against the
  ledger and returns the sequence number it was recorded under.
- **`InMemoryBookingLedger`.** Confirms with `Interlocked.Increment` over a
  sequence counter. It suspends with `Task.Yield()`, never a wall-clock delay,
  so a caller sees a genuine asynchronous suspend-and-resume without a test
  ever waiting on the clock.
- **`SailingBookingDesk`.** Accepts a `BookingRequest` through the ledger
  (`AcceptAsync`), accepts a batch concurrently (`AcceptAllAsync`), and
  reports whether the sailing still has room with a `ValueTask<bool>`
  (`HasCapacityAsync`) that completes synchronously because no ledger call is
  needed to answer it.

**What the tests prove.** `BookingDeskTests` covers the pattern's everyday
shape: a single acceptance returns a receipt carrying the ledger's sequence,
a batch of a thousand requests gets a thousand distinct sequence numbers with
no duplicates or gaps, a capacity check completes with `IsCompletedSuccessfully`
rather than ever going asynchronous, and a pre-cancelled token is honoured
before the ledger is touched.

`StacklessModelTests` is where the chapter's claim gets tested directly. A
`GatedBookingLedger` test double records the managed thread id captured in
the pre-await portion of `ConfirmAsync`, before its single shared
`TaskCompletionSource` gate is ever awaited, and exposes an `AllArrived` task
that completes the instant every confirmation has arrived at the gate.
`A_thousand_bookings_suspend_concurrently_without_a_thread_each` starts a
thousand `AcceptAsync` calls by enumerating `Select(...).ToList()` on the test
thread, waits for all thousand to arrive at the gate, and then asserts: all
thousand arrived, every one of them captured the *same* thread id before its
first await, and none of the thousand returned tasks has completed yet. That
is the stackless model in one assertion block: a thousand suspended state
machines sitting on the heap, never a thousand parked threads. Releasing the
gate resumes and completes all of them.
`A_booking_resumes_and_completes_after_the_awaited_confirmation` is the same
proof at its simplest, one booking at a time: the task is not complete while
suspended at the gate, and completes with the expected receipt once released.

Every gate in this example is a `TaskCompletionSource` created with
`TaskCreationOptions.RunContinuationsAsynchronously`, an `Interlocked` counter,
or a pre-cancelled `CancellationTokenSource`. Nothing here calls
`Thread.Sleep`, `Task.Delay`, or reads the wall clock, so the suite is
deterministic and fast on every run.

Shipping domain, no containers, pure BCL: no runtime package is needed to
demonstrate why the stackless model scales the way it does.

## Build and test

```sh
cd examples/33-why-dotnet-chose-stackless-async
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
