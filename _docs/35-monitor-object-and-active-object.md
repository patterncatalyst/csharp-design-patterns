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

By the end of this chapter you can:

- Wrap a shared, non-thread-safe servant in a Monitor Object using `System.Threading.Lock`, and say why a synchronous critical section is what makes it correct.
- State what CS1996 rejects, for both an `object` monitor and the `Lock` type, and connect that error to the move from Monitor Object to Active Object.
- Build an async-aware gate with `SemaphoreSlim(1, 1)` and name the two guarantees it still lacks.
- Implement an Active Object on a bounded channel with a `TaskCompletionSource`-backed future, and map it onto POSA2's six participants.
- Explain how a bounded channel turns a full write into deterministic backpressure, which a semaphore gate can't signal.

## Why this matters

A vessel's stowage plan is one shared, mutable structure: a fixed set of slots, each open or holding a consignment. A booking service, a re-stow tool reacting to a late change and an automated slotting pass all reserve on the same voyage at once, and each must see a consistent picture of free slots. If two callers both see an open slot and both assign to it, the vessel sails over capacity or a reefer consignment lands in a slot with no power, and the failure shows up at the dock.

`StowagePlan` is plain single-threaded code: a dictionary lookup, a loop over candidate slots, a check against `SlotRequirements` for reefer and tier, an assignment. The read-modify-write in `TryReserve` isn't thread-safe and shouldn't have to be; coordination belongs in a wrapper.

The requirement then grows. A reservation isn't real until a ledger write lands, and that write is I/O, which means an `await`. Four strategies follow that one requirement, and the point where the simplest stops working is a compiler diagnostic.

{% include excalidraw.html name="35-monitor-object-and-active-object" caption="Figure 35.1: One shared servant wrapped by four strategies in sequence, a synchronous lock, an illustrative lock that will not compile, an async semaphore gate, and a bounded queue that replaces the gate entirely." %}

## Pattern: Monitor Object

### The problem

Many threads call one servant whose operations are read-modify-write sequences. Without coordination, two calls interleave and hand out the same slot.

### The solution

**A Monitor Object serializes every call into a shared servant behind one gate, so each call sees the servant either fully updated or untouched.**

### How C# models it

`SynchronizedStowagePlan` holds a `System.Threading.Lock` for the shortest possible time around each call:

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

The `lock` statement recognizes a `Lock` field and compiles to its `EnterScope` API instead of locking on an arbitrary `object`. The gate's body is the call into `TryReserve` and nothing more. One thread runs the read-modify-write to completion; every waiting thread sees a servant that isn't halfway through an assignment.

Sixteen `Task.Run` callers released together through a `Barrier` never share a slot, and the servant's count matches the successful reservations. The bare servant called sequentially is correct too, because one thread never races itself. The Monitor Object adds correctness under overlapping calls, nothing more.

No `await` sits near the gate, and none is needed: choosing a slot is a fast, in-memory decision.

### The wall a durable reservation runs into

A durable reservation returns a `SlotAssignment` only after a ledger append, which is asynchronous. Widening the lock to cover it looks natural:

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

The compiler refuses it, so nothing ships this code. CS1996 fires the same way for an `object` gate and for `System.Threading.Lock`. A `lock` desugars to an enter call paired with a `try`/`finally` release, which assumes the method runs straight through. An `await` can suspend the method and resume it later, by default possibly on another thread, so the releasing thread might not be the acquiring one and the critical section would stay open for as long as the I/O takes.

Monitor Object was cataloged for synchronous method bodies, and C# enforces that boundary at compile time. The fix is a primitive built to be awaited.

### An async-aware gate with a semaphore

`SemaphoreStowagePlan` uses a `SemaphoreSlim` with one permit:

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

`WaitAsync(ct)` observes cancellation on entry: a pre-cancelled token throws `OperationCanceledException` before the servant is touched. The `finally` releases the permit however the method exits, so a failed ledger write can't strand later callers. Five hundred concurrent `ReserveAsync` calls through `Task.WhenAll` all get distinct slots, across a real asynchronous write.

It compiles and serializes correctly, but the permit now covers the ledger `await` too, and two guarantees are missing:

| Missing | Consequence |
| --- | --- |
| Ordering | `SemaphoreSlim` doesn't release waiters FIFO; the longest-waiting caller has no claim to go next |
| Visibility | Nothing can observe how many callers are waiting, so heavy load looks like light load and nothing upstream can shed work |

## Pattern: Active Object

### The problem

The critical section now contains I/O. A gate held across that I/O parks every caller with no ordering, no limit on how many pile up, and no signal back to producers.

### The solution

**An Active Object decouples calling a method from running it: callers enqueue a request and get a future, and one consumer runs requests against the servant in order.**

### How C# models it

`StowageActiveObject` removes the gate. A bounded channel is the queue, and one consumer drains it, so only one caller ever touches the servant and no lock is needed:

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

`ReserveAsync` awaits twice. The outer await completes when the request lands in the channel; the inner await completes when the consumer has run it against the servant and the ledger. `EnqueueAsync` is `internal` so a caller in the same assembly can hold a request mid-flight and inspect a full channel without a wall-clock wait.

`RunAsync` is the only code that calls `_servant`, and `await foreach` over `ReadAllAsync()` is the whole serialization mechanism. Behavior at the edges:

- **Cancelled while queued:** the request's task is cancelled before the servant sees it.
- **Cancelled during the write:** the ledger's `OperationCanceledException` cancels that request's task.
- **Ledger failure:** the exception goes to that request's task, and the loop keeps serving everything queued behind it.
- **Shutdown:** `DisposeAsync` completes the writer, `ReadAllAsync` ends after the last request drains, and the consumer task is awaited.

A thousand reservations through the Active Object all get distinct slots.

### Mapping POSA2's participants onto the channel

*Pattern-Oriented Software Architecture, Volume 2* lists six Active Object participants:

{% include excalidraw.html name="35-active-object-participants" caption="Figure 35.2: Proxy is ReserveAsync, Activation List is the bounded channel, and Servant is StowagePlan. Scheduler and Future are grayed out because they dissolve into await foreach and the language's own task." %}

| Participant | In `StowageActiveObject` | Written by hand? |
| --- | --- | --- |
| Proxy | `ReserveAsync` | Yes |
| Method Request | the private `ReserveRequest` record: consignment, requirements, token, completion source | Yes |
| Activation List | the bounded channel | Yes |
| Scheduler | the `await foreach` loop in `RunAsync` | No: a language construct |
| Servant | `StowagePlan`, unchanged | Yes, with no locking |
| Future | `Task<SlotAssignment?>` from a `TaskCompletionSource<SlotAssignment?>` | No: the runtime's task |

When POSA2 was written, a Scheduler meant hand-rolled dispatch logic, and a Future meant a hand-rolled type with its own blocking accessor and result bookkeeping. In modern C#, `await foreach` is the dispatch loop, and `Task<T>` with `TaskCompletionSource<T>` is a future with continuations, exception propagation and cancellation built in. Two of the six participants dissolve into the language and runtime.

### Deterministic backpressure from a bounded channel

The channel is created with `Channel.CreateBounded<ReserveRequest>` and `FullMode = BoundedChannelFullMode.Wait`. A write to a full channel neither throws nor drops: it suspends the caller until the consumer makes room.

{% include excalidraw.html name="35-channel-backpressure" caption="Figure 35.3: A one-slot channel fills while one write holds a gated ledger open. A third reservation's write waits until release lets the queue drain, completing its task." %}

With capacity one and a ledger that blocks until released, the sequence is deterministic:

1. The first request is in flight inside the consumer.
2. The second request fills the channel.
3. The third request's write doesn't complete; there's nowhere for it to land.
4. Releasing the ledger lets the consumer finish the first, drain the second, accept the third, and all three tasks complete.

That's what the semaphore gate can't do. A semaphore gives every waiter the same experience, with no visible queue and no limit. A bounded channel makes the queue a countable resource: at most one request waits behind the one being served, and when it's full, the caller of `ReserveAsync` feels it as an `await` that won't complete until there's room. The consumer's pace governs how fast producers add work, enforced by the channel.

## Build, run, observe

```
cd examples/35-monitor-object-and-active-object && dotnet test
```

All 8 tests pass, covering the four strategies: contention and sequential runs for the Monitor Object, five hundred concurrent callers and a cancelled token for the semaphore gate, and for the Active Object a thousand-reservation serialization run, the full-channel backpressure sequence, a request cancelled while queued, and a ledger failure that reaches its caller while the consumer keeps running. A gated ledger and `TaskCompletionSource` coordination replace wall-clock timing, so the results hold on every run.

## When not to use it

- **Monitor Object is still the default** when the critical section is short and synchronous: a dictionary update, a counter increment, a validation check with no I/O. A channel, completion-source bookkeeping and a consumer task that must start and stop cleanly buy nothing over `lock` when nothing inside the gate awaits.
- **Active Object isn't the ceiling.** It coordinates one servant in one process. It doesn't give supervision trees, location transparency, distributed state, or built-in retry and persistence. For actors addressed across machines, restarted on failure, or persisted and replayed, **Orleans**, **Akka.NET** and **Dapr actors** are the heavier .NET options. Using one to protect a single dictionary is the same mismatch in the other direction.

| Critical section | Use |
| --- | --- |
| Short, synchronous | `Lock`-guarded Monitor Object |
| Contains an `await`; waiter order and queue depth don't matter | `SemaphoreSlim(1, 1)` gate |
| Contains I/O, needs ordering and backpressure | Active Object on a bounded channel |
| Distributed, supervised or persisted | An actor framework |

<blockquote class="ai-sidebar" markdown="1">
**Specifying a thread-safe stowage plan.** "Make the stowage plan thread-safe" doesn't say whether the critical section awaits, or whether callers need ordering and backpressure. State it:

> `StowagePlan.TryReserve` is synchronous; the `IStowageLedger.AppendAsync` write after it awaits. Build `StowageActiveObject` around one `StowagePlan`: a private `ReserveRequest` record carrying a `TaskCompletionSource<SlotAssignment?>` created with `RunContinuationsAsynchronously`, a `Channel.CreateBounded` channel with `BoundedChannelFullMode.Wait`, and one `RunAsync` consumer that completes each request with a result, a cancellation or the ledger's exception and keeps running. `DisposeAsync` completes the writer and awaits the pump. Never put an `await` inside a `lock`.

Run `dotnet test --project StowageCoordination.Tests --filter ActiveObjectTests`; all 4 must pass.
</blockquote>

## For deeper coverage

- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue entry for both Monitor Object and Active Object, and the source of the six Active Object participants mapped onto `StowageActiveObject`.
- Microsoft Learn, [lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock): the language reference for `lock`, including why an `await` inside its body is a compile-time error.
- Microsoft Learn, [System.Threading.Lock](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock): the dedicated mutual-exclusion type `SynchronizedStowagePlan` guards its servant with.
- Microsoft Learn, [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels): the authority for `Channel.CreateBounded`, `BoundedChannelFullMode`, and the producer/consumer shape `StowageActiveObject` builds on.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
