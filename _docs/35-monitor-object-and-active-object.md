---
title: "Monitor Object and Active Object on Channel<T>"
order: 35
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "The largest gap in the literature: POSA2's patterns re-expressed on async and Channel<T>, where the language rejects Monitor Object at compile time and two participants dissolve into Task<T> and await foreach."
---

<!-- EXAMPLE (shipping domain): A StowagePlan whose agents concurrently reserve container slots. -->

## Learning objectives

- Wrap a shared, non-thread-safe servant in a Monitor Object using `System.Threading.Lock`, and explain why a purely synchronous critical section is the condition that makes it correct.
- State precisely what CS1996 rejects and why, for both an `object` monitor and the dedicated `Lock` type, and connect that compiler error to the shift from Monitor Object to Active Object.
- Build an async-aware gate with `SemaphoreSlim(1, 1)` that awaits inside `WaitAsync`/`try`/`finally`, and name the two guarantees it still does not give you.
- Implement an Active Object on a bounded channel with a `TaskCompletionSource`-backed future, and map its request, queue, and consumer loop onto POSA2's six Active Object participants.
- Explain how a bounded channel turns a full write into deterministic backpressure, and contrast that with a semaphore gate that offers no equivalent signal.

## Why this matters

A stowage plan for a single vessel is one shared, mutable structure: a fixed set of slots, each either open or already holding a consignment. Several agents reserving space on the same voyage, a booking service, a re-stow tool reacting to a late change, an automated slotting pass, all need to call into that one structure at the same time, and every one of those calls has to see a consistent picture of which slots are still free. The moment two callers both see an open slot and both assign a consignment to it, the vessel sails over capacity or a reefer consignment lands somewhere with no power, and that is a shipping failure discovered at the dock, not in a test run.

`StowagePlan` is written as plain, single-threaded code, a dictionary lookup, a loop over candidate slots, a check against `SlotRequirements` for reefer and tier, an assignment. None of that is thread-safe, and none of it should have to be. The read-modify-write in `TryReserve` is exactly the kind of logic a developer wants to read and verify without reasoning about interleaving at the same time. The coordination problem, letting many callers share that servant safely, belongs in a wrapper around it, not folded into the servant's own logic.

The full version of that coordination problem does not stay synchronous for long. A reservation a caller can see is not the same thing as a reservation that has survived: somewhere a ledger write has to land before the slot is really taken, and that write is I/O, which in modern C# means an `await`. The four stages in this chapter follow that one requirement, confirm a reservation durably, through four different coordination strategies, and the point where the simplest strategy stops working is not a judgment call. It is a compiler diagnostic.

{% include excalidraw.html name="35-monitor-object-and-active-object" caption="Figure 35.1: One shared servant wrapped by four strategies in sequence, a synchronous lock, an illustrative lock that will not compile, an async semaphore gate, and a bounded queue that replaces the gate entirely." %}

## Stage 1: Monitor Object with a synchronous gate

`SynchronizedStowagePlan` is the textbook Monitor Object: one gate, held for the shortest possible time, around every call into the servant.

```csharp
public sealed class SynchronizedStowagePlan
{
    private readonly StowagePlan _plan;
    private readonly Lock _gate = new();

    public SynchronizedStowagePlan(StowagePlan plan) => _plan = plan;

    public SlotAssignment? Reserve(ConsignmentId id, SlotRequirements req)
    {
        lock (_gate)
        {
            return _plan.TryReserve(id, req);
        }
    }
}
```

`System.Threading.Lock` is the dedicated mutual-exclusion type the `lock` statement recognizes directly, compiling to its own `EnterScope` API rather than the older pattern of locking on an arbitrary `object` field. Nothing about that choice changes the shape of this class: the body inside the gate is the entire call into `TryReserve`, nothing more, and that is what makes the pattern correct here. One thread enters, runs the read-modify-write to completion, and leaves; every other thread waiting on `_gate` sees a servant that is always either fully updated or not yet touched, never halfway through an assignment.

The concurrency test backs this with real contention rather than an assumption: sixteen `Task.Run` callers release through a `Barrier` so they all call `Reserve` as close to simultaneously as the scheduler allows, and the assertions check two things, that no two results share a slot and that the servant's own count matches the number of successful reservations. A second test runs the identical sixteen reservations through the bare, unsynchronized servant in a plain loop, which is correct too, because a single thread calling `TryReserve` one at a time never races with itself. The two tests together separate what the Monitor Object buys you, correctness under concurrent, overlapping calls, from what the servant already had, correctness under calls that never overlap at all.

This stage has no `await` anywhere near the gate and does not need one. The reservation decision, which slot to hand out, is entirely synchronous, so a plain lock is the right amount of mechanism: no asynchronous waiting to express, no queue to manage, just mutual exclusion around a fast, in-memory decision.

## Stage 2: the wall a reservation that must be durable runs into

Confirming a reservation durably means the caller should not get a `SlotAssignment` back until that assignment has been appended to a ledger, and appending to a ledger is asynchronous work. The natural next step looks like widening the existing lock body to cover that write too:

```csharp
// does not compile: CS1996 "Cannot await in the body of a lock statement"
lock (_gate)
{
    var assignment = _servant.TryReserve(id, req);
    if (assignment is not null)
        await _ledger.AppendAsync(assignment, ct); // CS1996 here
    return assignment;
}
```

This is illustrative only; nothing in the example ships this code, because the compiler refuses it. CS1996, "Cannot await in the body of a lock statement," fires identically whether the gate is an arbitrary `object` or the dedicated `System.Threading.Lock` type from Stage 1. The `lock` statement desugars to an enter call paired with a `try`/`finally` that releases the gate, and that pairing assumes the method runs straight through without suspending. An `await` can suspend the method mid-body and resume it later, possibly on a different thread by default, and there is no safe way to make "hold this gate across a suspension point" mean anything reliable: the thread that eventually calls the release might not be the thread that acquired the gate, and the "critical section" could, in principle, stay open for however long the awaited work takes.

Framed positively, this is the compiler closing a door before a team walks through it at runtime in production under load. Monitor Object was cataloged for synchronous method bodies, and C# enforces that boundary at compile time rather than leaving it as a convention someone has to remember. The fix is not a different kind of lock statement; it is a different coordination primitive, one built to be awaited.

## Stage 3: an async-aware gate with a semaphore

`SemaphoreStowagePlan` swaps the lock for a `SemaphoreSlim` sized to one permit, which gives the same one-caller-at-a-time guarantee but through an API that can be awaited.

```csharp
public async Task<SlotAssignment?> ReserveAsync(
    ConsignmentId id, SlotRequirements req, CancellationToken ct = default)
{
    await _gate.WaitAsync(ct);
    try
    {
        var assignment = _servant.TryReserve(id, req);
        if (assignment is not null)
            await _ledger.AppendAsync(assignment, ct);
        return assignment;
    }
    finally
    {
        _gate.Release();
    }
}
```

`WaitAsync(ct)` is the async counterpart to acquiring the gate, and it also means cancellation is observed on entry: a caller whose token is already cancelled never gets inside the `try` block at all, and `ReserveAsync` throws `OperationCanceledException` before touching the servant. The `finally` block is what makes this safe in the presence of an exception from either `TryReserve` or the ledger write: the permit is released no matter how the method exits, so a failed reservation cannot leave every later caller waiting forever.

The concurrency test drives five hundred concurrent `ReserveAsync` calls through `Task.WhenAll` and checks the same two properties as Stage 1, every result has a distinct slot and the servant's count matches the successful reservations, now proven across a real asynchronous ledger write rather than a purely synchronous critical section. A second test confirms the pre-cancelled-token behavior directly.

This stage compiles, and it serializes correctly. It is also where the design starts to cost more than it looks like at first glance. The single permit is held for the entire method body, which now includes the `await` on the ledger. While one caller's write is in flight, every other caller is parked on `WaitAsync`, and `SemaphoreSlim` makes no promise about the order in which parked callers are released: there is no FIFO guarantee, so a caller that has been waiting the longest has no assurance it goes next. There is also no way for a caller, or for anything else, to observe how deep the queue of waiters has gotten. A system under heavy load and a system under light load look identical from the outside: callers just wait, with no signal to shed load or apply backpressure anywhere upstream. The lock scope grew from "touch a dictionary" to "touch a dictionary and wait on I/O," and the semaphore treats both the same way it always did, as one thing to protect, with nothing extra to say about the part of that scope that takes real time.

## Stage 4: Active Object on a bounded channel

`StowageActiveObject` removes the gate entirely. In its place is a bounded channel acting as a queue, and exactly one consumer draining it, which means the servant goes back to being touched by a single caller at a time without any lock protecting it.

```csharp
public async Task<SlotAssignment?> ReserveAsync(
    ConsignmentId id, SlotRequirements req, CancellationToken ct = default) =>
    await await EnqueueAsync(id, req, ct);

internal async Task<Task<SlotAssignment?>> EnqueueAsync(
    ConsignmentId id, SlotRequirements req, CancellationToken ct = default)
{
    var tcs = new TaskCompletionSource<SlotAssignment?>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    await _channel.Writer.WriteAsync(new ReserveRequest(id, req, tcs, ct), ct);
    return tcs.Task;
}

private async Task RunAsync()
{
    await foreach (var req in _channel.Reader.ReadAllAsync())
    {
        if (req.Ct.IsCancellationRequested)
        {
            req.Completion.TrySetCanceled(req.Ct);
            continue;
        }

        try
        {
            var assignment = _servant.TryReserve(req.Consignment, req.Requirements);
            if (assignment is not null)
                await _ledger.AppendAsync(assignment, req.Ct);
            req.Completion.TrySetResult(assignment);
        }
        catch (OperationCanceledException)
        {
            req.Completion.TrySetCanceled(req.Ct);
        }
        catch (Exception ex)
        {
            req.Completion.TrySetException(ex);
        }
    }
}
```

`ReserveAsync` is a double `await`, and the split between the two is the design's seam. The outer `await` is on `EnqueueAsync`, which completes once the request's write has landed in the channel; the inner `await` is on the `Task` the `TaskCompletionSource` hands back, which completes once the one consumer has processed that request against the servant and the ledger. `EnqueueAsync` is marked `internal` rather than private because that split is also how a test pins a request mid-flight and asserts on a channel that is full without any wall-clock wait: enqueue, observe, then await the result separately.

`RunAsync` is the only code that ever calls into `_servant`, and `await foreach` over `_channel.Reader.ReadAllAsync()` is the entire serialization mechanism. Nothing needs a lock, because structurally nothing else can reach the servant: one loop, one request at a time, run to completion before the next one starts. Cancellation is checked twice, once before a request is even handed to the servant in case it was already cancelled while queued, and once around the work itself in case the ledger observes a cancelled token mid-write. A caught `Exception` from a failing ledger write is routed to that request's own completion source rather than rethrown out of the loop, which is what lets the consumer keep running for every request still queued behind a failure instead of taking the whole Active Object down with it. `DisposeAsync` completes the channel's writer, which lets `ReadAllAsync` end cleanly once the last queued request drains, and then awaits the consumer task to confirm it stopped.

## Mapping POSA2's Active Object onto the channel

*Pattern-Oriented Software Architecture, Volume 2* catalogs Active Object with six participants, and every one of them has a concrete counterpart in `StowageActiveObject`.

{% include excalidraw.html name="35-active-object-participants" caption="Figure 35.2: Proxy is ReserveAsync, Activation List is the bounded channel, and Servant is StowagePlan. Scheduler and Future are grayed out because they dissolve into await foreach and the language's own task." %}

Proxy is `ReserveAsync` itself, the method a caller sees and calls, which looks like an ordinary asynchronous method while hiding where the real work happens. Method Request is the private `ReserveRequest` record, an immutable bundle of the consignment, the requirements, the cancellation token, and the completion source that will eventually carry the result, placed on the queue as one value. Activation List is the bounded channel, holding every request that has been accepted but not yet processed. Scheduler is the `await foreach` loop inside `RunAsync`, pulling one request at a time and running it to completion before pulling the next. Servant is `StowagePlan`, completely unchanged from Stage 1, because exclusive access is now structural rather than enforced by a gate. Future is the `Task<SlotAssignment?>` a caller awaits, produced by a `TaskCompletionSource<SlotAssignment?>`.

The thesis worth sitting with is which of those six participants a developer writes as a named type. Proxy, Method Request, and Activation List are all concrete, hand-written pieces of this class. Scheduler and Future are not. In the environment POSA2 was cataloging, both had to be built from scratch: a scheduler meant hand-rolled dispatch logic deciding what runs next, and a future meant a hand-rolled type with its own blocking accessor and its own bookkeeping for a result that might not exist yet. In modern C#, `await foreach` collapses the Scheduler's dispatch logic into a single loop construct the language already understands, and `Task<T>` paired with `TaskCompletionSource<T>` already is a future, complete with continuation chaining, exception propagation, and cancellation support built into the runtime. Two of POSA2's six participants did not get reimplemented here. They dissolved into language and library features a developer already has just by writing `async`, `await`, and `Task<T>`.

## Deterministic backpressure from a bounded channel

The channel backing `StowageActiveObject` is created with `Channel.CreateBounded<ReserveRequest>` and `FullMode = BoundedChannelFullMode.Wait`, which means a write to a full channel does not throw and does not drop the request. It awaits, suspending the caller until the consumer drains enough room for one more entry.

{% include excalidraw.html name="35-channel-backpressure" caption="Figure 35.3: A one-slot channel fills while one write holds a gated ledger open. A third reservation's write waits until release lets the queue drain, completing its task." %}

The backpressure test pins this deterministically rather than relying on timing. A ledger that blocks mid-write until the test releases it holds one request inside the scheduler, in flight but not yet complete. A second request's write fills the channel, whose capacity is set to one for the test. A third request's write is issued but not awaited, and the test asserts directly that its task has not completed, because there is nowhere in a capacity-one channel for that third request to land while the first two occupy the only slot and the consumer. Only after the test releases the gated ledger does the consumer finish the first request, drain the channel, accept the third write, and let all three tasks complete.

This is the gap Stage 3 cannot close. A semaphore gate gives every waiting caller the same experience regardless of how many callers are ahead of it: block, then eventually run, with no visible queue and no finite limit on how many callers can pile up waiting. A bounded channel makes the queue a real, countable resource. A capacity of one means at most one request waits behind the one being served, and the moment that is full, the pressure becomes visible to whatever called `ReserveAsync`, as an `await` that does not complete until the system has made room. That is backpressure in the literal sense: the slow consumer's pace governs how fast producers are allowed to add work, enforced by the channel itself rather than by a developer remembering to check a queue depth somewhere.

## Build, run, observe

```
cd examples/35-monitor-object-and-active-object && dotnet test
```

```
Test run summary: Passed!
  total: 8
  failed: 0
  succeeded: 8
  skipped: 0
```

The suite covers all four stages: the `Barrier`-driven contention test for `SynchronizedStowagePlan` alongside a sequential run of the bare servant, the five-hundred-caller concurrency test and the cancellation test for `SemaphoreStowagePlan`, and four tests for `StowageActiveObject`, a thousand-reservation serialization check, the gated backpressure test that pins a full channel mid-drain, a cancelled-while-queued request, and a ledger failure that surfaces to its caller without stopping the scheduler for anyone queued behind it. Every test is deterministic: the gated ledger and `TaskCompletionSource` coordination replace any reliance on wall-clock timing, so the same assertions hold on every run regardless of how the scheduler happens to interleave the underlying tasks.

## When not to use it

A plain `Lock`-guarded Monitor Object is still the right default when the critical section is purely synchronous and short, a dictionary update, a counter increment, a validation check with no I/O inside it. Reaching for a bounded channel and a consumer loop to protect work that never awaits anything is solving a problem that does not exist yet: the queue, the completion source bookkeeping, and the dedicated consumer task all cost real code and a background task that has to be started and shut down correctly, and none of that buys anything over a `lock` statement when nothing inside the gate ever needs to be awaited.

An Active Object on a bounded channel is also not the ceiling. It coordinates access to one servant inside one process, and it does that well, but it does not give a team supervision trees, location transparency, distributed state, or built-in retry and persistence policies. When the coordination problem grows past a single in-process servant, into actors that need to be addressed across machines, restarted automatically on failure, or persisted and replayed, **Orleans**, **Akka.NET**, and **Dapr actors** are the heavier .NET options built for exactly that scope. Reaching for one of them to protect a single shared dictionary is the same mismatch in the other direction: real infrastructure for a problem a bounded channel already solves.

<blockquote class="ai-sidebar" markdown="1">
An assistant is a reasonable pair for the mechanical parts of an Active Object: scaffolding the method request record, wiring a bounded channel with the right capacity and full mode, and writing the consumer loop's cancellation and exception handling around a known shape like `RunAsync` above. What has to stay a human decision is which rung of the ladder the problem needs, a plain lock, an async semaphore gate, a channel-based Active Object, or a full actor framework. An assistant asked to "make this thread-safe" will often reach straight for the heaviest tool that compiles, because a queue and a consumer loop look more sophisticated than a `lock` statement, without ever asking whether the critical section even contains an `await`, or whether supervision and distribution are problems this system has at all.
</blockquote>

## For deeper coverage

- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue entry for both Monitor Object and Active Object, and the source of the six Active Object participants this chapter maps onto `StowageActiveObject`.
- Microsoft Learn, [lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock): the language reference for `lock`, including why an `await` inside its body is a compile-time error.
- Microsoft Learn, [System.Threading.Lock](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock): the dedicated mutual-exclusion type `SynchronizedStowagePlan` guards its servant with.
- Microsoft Learn, [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels): the authority for `Channel.CreateBounded`, `BoundedChannelFullMode`, and the producer/consumer shape `StowageActiveObject` builds on.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
