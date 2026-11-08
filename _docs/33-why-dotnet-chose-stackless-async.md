---
title: "Why .NET chose stackless async"
order: 33
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "The framing chapter: green threads prototyped and rejected, and why that choice shapes every pattern that follows."
---

<!-- EXAMPLE (shipping domain): Booking throughput against a single sailing. -->

## Learning objectives

By the end of this chapter you can:

- State what a suspended `await` costs at runtime: a small heap-allocated state machine, not a parked thread.
- Trace `SailingBookingDesk.AcceptAsync` from straight-line source to a state machine that suspends at its one `await` and resumes when `IBookingLedger.ConfirmAsync` completes.
- Explain why `AcceptAllAsync` can start a thousand bookings from one calling thread without a thousand threads.
- Say why `HasCapacityAsync` returns `ValueTask<bool>` and completes with no `Task` allocation.
- Name the .NET team's 2022 green-threads prototype, what it tried to do differently, and why the team kept the stackless model.

## Why this matters

An `async` method in C# doesn't run on its own thread. It compiles into a state machine, a small heap object that records where execution paused and the locals it needs to resume. That was a design decision with a real alternative, and .NET prototyped the alternative. The stackless model explains why a booking desk holds a thousand in-flight confirmations on a handful of thread-pool threads, why a `CancellationToken` check matters before the first `await`, and why `ValueTask<TResult>` exists alongside `Task<TResult>`.

`SailingBookingDesk` accepts consignments onto one sailing by confirming each against an `IBookingLedger`. A real confirmation is I/O-bound: a network round trip or a database write, during which the initiating thread has nothing to do. `AcceptAsync` awaits one confirmation; `AcceptAllAsync` runs a batch concurrently.

{% include excalidraw.html name="33-why-dotnet-chose-stackless-async" caption="Figure 33.1: A thousand SailingBookingDesk bookings suspend at IBookingLedger.ConfirmAsync and resume later, each one costing a small heap state machine rather than a dedicated thread." %}

## A method that compiles into a state machine

`AcceptAsync` reads as sequential code:

```csharp
public async Task<BookingReceipt> AcceptAsync(BookingRequest request, CancellationToken ct = default)
{
    var seq = await ledger.ConfirmAsync(request.ConsignmentId, ct);
    Interlocked.Increment(ref _accepted);
    return new BookingReceipt(request.ConsignmentId, seq);
}
```

The compiler rewrites it into a type implementing a state-machine interface, with:

- a numbered state field recording which `await`, if any, it's suspended at;
- fields for every parameter and local needed across a suspension (`request`, `ct`, later `seq`);
- a `MoveNext` method holding the original logic, split at each `await`.

Calling `AcceptAsync` runs `MoveNext` until the method finishes or reaches an `await` on incomplete work. Then `MoveNext` returns, the calling thread is free, and the state machine schedules `MoveNext` again for when `ConfirmAsync`'s task completes.

The locals live in fields because no stack frame survives to hold them: the calling thread has moved on. A suspended `await` is an object sized to one method's locals, tens or hundreds of bytes, against the megabyte-scale stack a dedicated thread reserves. That's the stackless model's defining property.

## The alternative the .NET team tried and set aside

In 2022 the .NET team prototyped stackful coroutines under the name green threads: lightweight, runtime-scheduled threads with their own growable stacks. A green thread can suspend several frames below the call that blocks, because its whole stack travels with it. The appeal was that existing synchronous code, including library code a team doesn't control, could scale without `async` and `await` threaded through every method on the path.

The team wound the prototype down the following year. Its published results, in the same `dotnet/runtimelab` repository, favored the model already in production:

| | Stackful green thread | Stackless state machine |
| --- | --- | --- |
| Held across a suspension | The whole growable call stack | One method's locals |
| Can suspend deep in a call chain | Yes | Only at an `await` |
| Code changes needed | None in principle | `async`/`await` on the call path |
| Footprint at 100,000 outstanding operations | 100,000 stacks | 100,000 small objects |
| Status | Prototype, wound down | Shipping since C# 5, tuned, used across the base class library |

{% include excalidraw.html name="33-stackless-vs-stackful" caption="Figure 33.2: A stackful green thread carries a full growable call stack across every suspension; a stackless async method carries only the small state machine its own locals need." %}

## A thousand bookings, one calling thread, no thread each

`AcceptAllAsync` makes the payoff observable:

```csharp
public async Task<IReadOnlyList<BookingReceipt>> AcceptAllAsync(
    IReadOnlyCollection<BookingRequest> requests, CancellationToken ct = default)
{
    var tasks = requests.Select(r => AcceptAsync(r, ct)).ToList();
    return await Task.WhenAll(tasks);
}
```

`ToList()` enumerates on the calling thread. Each `AcceptAsync` runs to its `await`, suspends and hands back an incomplete `Task`, and the loop moves on. When `ToList()` returns, every booking has started and every one is suspended at the same point, with the calling thread never blocked. `Task.WhenAll` then suspends `AcceptAllAsync` until all of them finish.

A gated ledger demonstrates it. Every confirmation parks behind one shared `TaskCompletionSource`, and the ledger records which managed thread reached each confirmation before its await. With a thousand bookings started:

- all thousand reach the gate;
- `GatedBookingLedger.DistinctPreAwaitThreadCount` is 1, so they all started from one thread;
- none has completed until the gate opens.

A stackful model would need a thousand growable stacks for that; the stackless model uses a thousand small objects.

{% include excalidraw.html name="33-suspended-without-a-thread" caption="Figure 33.3: A thousand booking requests start from one thread, suspend at the ledger gate with zero threads held, and only resume once the test releases them together." %}

A single booking shows the resume half. It stays incomplete until the gate releases, then `MoveNext` runs again after the `await`, with `request` and the new `seq` available from the state machine's fields as if they were locals. None of this shows in the source: write the method as if it runs straight through, and the compiler carries the state across suspensions.

## Skipping the state machine entirely with ValueTask

`HasCapacityAsync` has no `async` keyword and no `await`:

```csharp
public ValueTask<bool> HasCapacityAsync(CancellationToken ct = default) =>
    new(Volatile.Read(ref _accepted) < sailingCapacity);
```

It reads a counter and compares it with a limit, finishing before it returns. A `Task<bool>` would work at every call site but allocate a heap object for a result that was never asynchronous. `ValueTask<TResult>` is a struct that holds a completed result with no allocation and wraps a real `Task<TResult>` only when the work goes asynchronous. The returned value reports `IsCompletedSuccessfully` before anyone awaits it: no suspension, and no state machine built for the call.

## Build, run, observe

```sh
cd examples/33-why-dotnet-chose-stackless-async
dotnet test
```

All 6 tests pass, and `scripts/verify.sh` at the repo root runs them with every other example. They cover a ledger-sequenced receipt for one booking, distinct sequence numbers across a thousand concurrent bookings, the synchronous capacity check, a cancelled token throwing before any confirmation starts, and the thousand-bookings-from-one-thread suspension.

## When not to use it

- **CPU-bound work.** A routing calculation over a large sailing schedule, or a hash over a large manifest, has no idle wait to release the thread during. Declaring it `async` adds a state machine around work that never suspends. Use parallelism across threads instead.
- **Async-over-sync.** `Task.Run` around a blocking call moves the block onto a pool thread, which then waits for the whole call. That burns a pool thread and adds a scheduling hop. The in-memory `ConfirmAsync` earns its signature because `Task.Yield` suspends it and frees the thread.
- **Fire-and-forget.** An unawaited task drops the one guarantee `Task` provides: knowing when the work finished and whether it threw. A desk that discarded `ConfirmAsync`'s task would lose ledger failures, or see them later as unobserved task exceptions with no link to the booking. `AcceptAsync` always awaits and propagates success, cancellation or failure to its caller.

<blockquote class="ai-sidebar" markdown="1">
An assistant handles two narrow jobs well: converting a synchronous method to `async`/`await` through a call chain without missing a caller, and scanning a body for blocking calls (a synchronous file read, `.Result`, `.Wait()`, `Thread.Sleep`) that stall the resuming thread. Neither answers whether a method should be `async` at all: that depends on whether the work is I/O-bound, with an idle period worth releasing the thread for, or CPU-bound. Check `HasCapacityAsync`-style methods by hand. Asked to "make it async," an assistant may wrap one in `Task.Run` or `Task.FromResult` when `ValueTask<bool>` already serves it with no allocation.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Asynchronous programming with async and await](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/): the authority for the `async`/`await` language feature and the state-machine transformation behind `AcceptAsync`.
- Microsoft Learn, [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap): the task-based async model `AcceptAsync` and `AcceptAllAsync` are written against.
- Microsoft Learn, [ValueTask\<TResult\> Struct](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1): the authority for when a `ValueTask<TResult>` return avoids the allocation `HasCapacityAsync` sidesteps.
- `dotnet/runtimelab`, [Green Thread Experiment](https://github.com/dotnet/runtimelab/issues/2057): the .NET team's 2022 tracking issue for the stackful coroutine prototype known as green threads.
- `dotnet/runtimelab`, [Green Thread Experiment Results](https://github.com/dotnet/runtimelab/issues/2398): the team's own writeup of why the prototype was wound down in favor of the stackless model.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
