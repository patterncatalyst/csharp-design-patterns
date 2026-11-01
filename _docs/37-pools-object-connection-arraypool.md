---
title: "Pools: object, connection, and ArrayPool<T>"
order: 37
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "Resource-management patterns for pooling connections and buffers that are scarce enough to matter."
---

<!-- EXAMPLE (shipping domain): A carrier gateway connection pool. -->

## Learning objectives

- Build a fixed-size async connection pool over a `Channel<T>`, where the channel is both the storage for idle connections and the wait queue a renter suspends on when the pool is empty.
- Explain why an exhausted pool suspending `RentAsync` is correct backpressure rather than a defect, and trace exactly which waiting caller a returned connection goes to.
- Wrap a borrowed resource in an RAII-style lease so a `using` block guarantees return even when the caller throws.
- Rent, use, and return a scratch buffer through `ArrayPool<byte>`, and state precisely why `clearArray: true` matters when the buffer held carrier or EDI data.
- Judge when buffer reuse through an array pool is worth the added complexity against allocating a fresh array per call.

## Why this matters

Most objects in a C# program are cheap. A `List<T>`, a DTO, a small record, allocating one costs a few nanoseconds and the garbage collector reclaims it without anyone noticing. Pooling that kind of object buys nothing: the pool's bookkeeping, a rent call, a return call, a place to store idle instances, costs more than the allocation it is trying to avoid. The pattern only pays for itself when the thing being managed is scarce enough to matter: expensive to create, limited in count, or large enough that repeated allocation shows up as real, measurable pressure on the garbage collector.

A connection to a carrier gateway is scarce in the first sense. Establishing one means a handshake and authentication with a remote system, work a caller does not want to repeat for every manifest it sends. A carrier's own infrastructure, or a contract with them, usually also caps how many connections a shipper can hold open at once, so the count itself is bounded independent of how much traffic the shipper wants to push through it. A large scratch buffer used to serialize a manifest is scarce in the second sense: not limited in count, but expensive enough per allocation, and frequent enough in a hot serialization path, that allocating and discarding one on every call turns into visible garbage-collector churn.

Both problems share the same shape: acquire a resource, use it, give it back, and make the giving-back automatic enough that a caller cannot forget. *Pattern-Oriented Software Architecture, Volume 3: Patterns for Resource Management* is the catalogue for exactly this shape, and it names the pieces this chapter builds: a Resource Pool that holds a bounded set of reusable instances, Eager Acquisition that creates the pool's full membership up front rather than on first demand, and a Resource Lease that ties a borrowed resource's lifetime to a scope the caller cannot exit without releasing it. None of that is C#-specific; what is specific here is how little code the modern runtime needs to express it. A `Channel<T>` already understands "suspend the reader until a writer shows up," so a connection pool does not need a hand-rolled wait queue, and `ArrayPool<T>` already understands bucketed array reuse, so a buffer pool does not need to be written at all, only used correctly.

{% include excalidraw.html name="37-pools-object-connection-arraypool" caption="Figure 37.1: A fixed-size connection pool hands out connections and takes them back on return; a scratch buffer pool reuses memory the same way." %}

## A fixed-size connection pool over a channel

`GatewayConnectionPool` is a pool of `IGatewayConnection` instances, each one standing in for an established session with a carrier gateway. Its constructor takes a fixed `size` and a `connectionFactory`, and it calls that factory exactly `size` times, writing every resulting connection into an unbounded `Channel<IGatewayConnection>` before the constructor returns. That is Eager Acquisition: the pool's full membership exists the moment construction completes, rather than growing lazily as callers show up. `Size` reports that fixed count for the life of the pool, and `Available` reports how many of those connections are currently idle, which is just the channel reader's `Count`.

```csharp
public sealed class GatewayConnectionPool : IAsyncDisposable
{
    private readonly Channel<IGatewayConnection> _available = Channel.CreateUnbounded<IGatewayConnection>();

    public GatewayConnectionPool(int size, Func<int, IGatewayConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        Size = size;
        for (var i = 0; i < size; i++)
            _available.Writer.TryWrite(connectionFactory(i));
    }

    public int Size { get; }

    public int Available => _available.Reader.Count;

    public ValueTask<IGatewayConnection> RentAsync(CancellationToken ct = default) =>
        _available.Reader.ReadAsync(ct);

    public void Return(IGatewayConnection connection) => _available.Writer.TryWrite(connection);
}
```

The channel is doing two jobs with one field. It is the storage for every connection that is not currently rented out, and it is the wait queue for a caller that arrives when nothing is idle. `RentAsync` is nothing more than `_available.Reader.ReadAsync(ct)`: reading from a channel that has an item completes immediately with that item, and reading from an empty channel suspends the reader until a writer supplies one. `Return` is nothing more than `_available.Writer.TryWrite(connection)`, which both deposits the connection back into the idle set and, if a reader happens to be parked waiting, completes that reader's pending read directly. A test confirms that distinct rents really do return distinct connections: renting all three connections out of a pool of three produces three different `Id` values, proving the factory is called once per slot and the channel is not silently handing out the same instance twice.

## Exhaustion is backpressure, not a bug

A pool sized smaller than demand will, by construction, run out. What happens next is the entire point of pooling a scarce resource rather than just allocating more of it: a renter that arrives when `Available` is already zero does not get an exception, a null, or a busy-wait loop. It gets a `ValueTask` that has not completed yet, suspended on the same channel read every other renter uses, until a `Return` call somewhere else in the program hands a connection back.

{% include excalidraw.html name="37-connection-pool-exhaustion" caption="Figure 37.2: A two-connection pool leaves a third rent incomplete until a connection comes back, and that exact connection goes to the caller who was already waiting." %}

The test for this proves the suspension without relying on a timeout or a sleep. It rents both connections out of a two-connection pool, confirms `Available` is zero, then starts a third `RentAsync` call and asserts `pendingC.IsCompleted` is false, a direct, deterministic check that the task really is parked rather than merely slow. Only after that assertion does the test call `Return` on one of the first two connections, and only then does `pendingC` complete. A companion test goes one step further and checks identity: the connection the waiting renter receives is the exact instance that was just returned, `Assert.Same(a, await pendingC)`, which confirms the channel hands a freed resource to whoever has been waiting longest rather than, say, re-queuing it behind the current caller. A third test cancels a pending rent through its own `CancellationTokenSource` while it is still parked and confirms it throws `OperationCanceledException` rather than hanging forever: a caller that gives up on waiting is never left stranded on an incomplete task.

This is backpressure, not a bug. If the pool grew without limit to meet demand, or if `RentAsync` threw the moment nothing was idle, the fixed size would stop meaning anything, and the scarce resource it is supposed to protect, whatever cap the carrier's own infrastructure places on concurrent connections, would be meaningless too. A caller that has to wait is getting a clear signal: the system is at capacity right now, and the correct response is to wait your turn, not to force a connection into existence that the far end was never provisioned to accept.

## The lease pattern: RentLeaseAsync and using

Calling `RentAsync` directly works, but it leaves returning the connection entirely up to the caller remembering to call `Return`, in every code path, including the ones that throw. `GatewayLease` closes that gap. It is a `readonly record struct` holding the pool and the connection it borrowed, and its only behavior is `IDisposable.Dispose`, which calls `Pool.Return(Connection)`.

```csharp
public readonly record struct GatewayLease(GatewayConnectionPool Pool, IGatewayConnection Connection)
    : IDisposable
{
    public void Dispose() => Pool.Return(Connection);
}
```

`RentLeaseAsync` on the pool is the one line that ties the two together: `new(this, await RentAsync(ct))`. A caller who writes `using var lease = await pool.RentLeaseAsync(ct);` gets the connection back through `lease.Connection`, and the moment that `using` block ends, by falling off the end normally or by an exception propagating out, `Dispose` runs and the connection goes back to the pool. The test for this is a direct before-and-after: inside the `using` block, `Available` is zero because the pool's only connection is out on lease; once the block exits, `Available` is back to one. A record struct is the right shape for a lease like this because a lease is naturally compared by what it holds rather than by reference identity, and because there is no heap allocation beyond the connection itself, the lease is just a stack-resident pairing of a pool reference and a connection reference.

This is the same shape POSA3 calls a Resource Lease: a value whose own lifetime, scoped by `using`, is what releases the resource, so "forgetting to return a connection" stops being a category of bug a caller can even write by accident, short of discarding the lease outright without ever disposing it.

## Object pools beyond a single connection type

`GatewayConnectionPool` is purpose-built: it understands exactly one kind of resource, and its rent operation can suspend a caller when nothing is available. For the more general case, pooling plain objects that are not scarce in the connection sense, just expensive enough to construct that reuse is worth the bookkeeping, `Microsoft.Extensions.ObjectPool` is .NET's own answer. Its `ObjectPool<T>` exposes a synchronous `Get`/`Return` pair, backed by a `PooledObjectPolicy<T>` that controls how an object is created and how it is reset before going back on the shelf, and ASP.NET Core itself uses exactly this to recycle `StringBuilder` instances rather than allocating one per request.

That synchronous `Get`/`Return` shape is also precisely why this chapter's example does not reach for it. `Microsoft.Extensions.ObjectPool` does not place a limit on how many objects it will hand out, only on how many it retains for reuse; a caller that calls `Get` when the pool is empty simply gets a freshly constructed instance rather than waiting. That is the right behavior for a `StringBuilder`, where manufacturing one more instance on demand is harmless, and the wrong behavior for a carrier connection, where the whole point of the fixed size is that the pool must never manufacture a connection the carrier's infrastructure was not provisioned to accept. `GatewayConnectionPool`'s channel-backed wait is a considered divergence from that default: a general object pool trades a hard cap for always returning promptly, and a connection pool over a strictly rationed resource needs the opposite trade.

## Reusing buffers with an array pool

`ManifestWriter` solves a different flavor of the same problem: serializing a `ManifestLine` collection into the wire format a carrier gateway expects, `Code=Value` per line, UTF-8 encoded, without allocating a fresh scratch array on every call.

```csharp
public byte[] Serialize(IReadOnlyList<ManifestLine> lines)
{
    ArgumentNullException.ThrowIfNull(lines);

    var upperBound = UpperBoundBytes(lines);
    var scratch = _pool.Rent(upperBound);
    try
    {
        var written = 0;
        foreach (var line in lines)
        {
            written += Encoding.UTF8.GetBytes(line.Code, scratch.AsSpan(written));
            scratch[written++] = (byte)'=';
            written += Encoding.UTF8.GetBytes(line.Value, scratch.AsSpan(written));
            scratch[written++] = (byte)'\n';
        }

        return scratch.AsSpan(0, written).ToArray();
    }
    finally
    {
        _pool.Return(scratch, clearArray: true);
    }
}
```

`UpperBoundBytes` walks the input once and computes a deterministic worst-case length: the maximum UTF-8 expansion of every code and value, plus one byte for each `=` and `\n` separator. That upper bound is what gets handed to `_pool.Rent`, which is the constructor-injected `ArrayPool<byte>` rather than a hard-coded reference to `ArrayPool<byte>.Shared`, so a test can substitute a recording pool and observe rent and return behavior directly instead of fighting the shared pool's process-wide, non-deterministic bucket contents. Encoding happens directly into the rented array through `AsSpan`, tracking exactly how many bytes have been written so far, and only the final `scratch.AsSpan(0, written).ToArray()` call allocates the array the caller keeps, trimmed to the real length rather than the worst-case one. The `try`/`finally` guarantees the scratch buffer goes back to the pool even if an encoding step throws partway through.

{% include excalidraw.html name="37-arraypool-buffer-reuse" caption="Figure 37.3: Renting a buffer, using it, and returning it cleared means the next rent gets back the same array instead of a fresh allocation." %}

A recording test double confirms the reuse directly rather than inferring it from timing or memory counters. It wraps a dedicated `ArrayPool<byte>.Create()` instance, never the shared pool, and records every array it hands out and takes back. Serializing the same sample manifest twice in a row shows the array returned after the first call is the exact array rented for the second call, `Assert.Same(firstReturnedArray, secondRentedArray)`, and that the two calls produce identical output bytes despite sharing physical memory between them. A round trip through `ManifestWriter.Parse`, the inverse of `Serialize`, confirms the reused buffer's contents decode back to the original lines correctly, closing the loop on correctness, not just reuse.

## Why clearArray matters for carrier data

`ArrayPool<T>.Return` takes a `clearArray` argument, and `ManifestWriter` always passes `true`. Without it, a returned array keeps whatever bytes were written into it the last time it held a manifest, sitting in the pool's bucket until the next `Rent` call hands that same backing array to a different caller. If that next caller's manifest happens to be shorter than the previous one, the trailing bytes of the array past where the new encoding stopped still hold the previous manifest's carrier name, consignment weight, or destination, bytes that `Serialize` never touches again because it only returns the span up to `written`. That stale tail cannot leak through `ManifestWriter.Serialize` itself, since the return value is always trimmed to the exact length written. The risk is anyone else who gets the same backing array from the pool and reads past the bounds a correct caller would respect, whether through a bug, a cast, or code that assumes the full rented length is meaningful data. Clearing the array on return removes that possibility entirely: every rented array a caller receives starts from a known, zeroed state, and freight data from one manifest never has a physical path into memory a different manifest's serialization might expose. The recording test asserts `pool.LastReturnedCleared` is true specifically to pin this behavior, because clearing is an opt-in argument, not a default, and a refactor that quietly dropped it would otherwise pass every other test in the suite.

## When buffer reuse pays off versus allocating fresh

Buffer reuse through an array pool is worth the added complexity when three conditions hold together: the array is large enough that allocating and discarding it repeatedly shows up as garbage-collector pressure, the code path that needs it runs often enough for that pressure to matter, and the buffer's lifetime is short and strictly scoped, rented, filled, read, returned, within a single call rather than held indefinitely. `ManifestWriter.Serialize` fits all three: it runs on every outbound manifest, the scratch buffer is only alive for the duration of one `Serialize` call, and the computed upper bound means repeated calls with similarly sized manifests rent from the same size bucket and get the same backing array back, which is exactly what the reuse test observes.

The trade stops paying off outside that shape. A buffer rented once and held for the life of an object defeats the purpose, since nothing else can reuse it while it is checked out indefinitely, and the pool ends up functioning as a slower allocator with extra bookkeeping on top. A caller whose buffer sizes vary wildly from one call to the next spreads rentals across many of the pool's internal size buckets instead of concentrating reuse into one, which looks like pooling but behaves more like allocating fresh arrays from a slightly more complicated allocator. And for small, short-lived arrays, a few dozen bytes, a handful of elements, the generational garbage collector already reclaims them cheaply enough that renting and returning costs more in code and indirection than it saves in allocation pressure. The deciding question is always whether the array is both large and frequent; `ManifestWriter`'s scratch buffer is both, which is why it is the one piece of this chapter's example built around `ArrayPool<T>` rather than a plain `new byte[...]` on every call.

## Build, run, observe

```
cd examples/37-pools-object-connection-arraypool && dotnet test
```

```
Test run summary: Passed!
  total: 9
  failed: 0
  succeeded: 9
  skipped: 0
```

The suite covers both halves of the chapter. For `GatewayConnectionPool`: a rented connection comes from the pool with a distinct identity, every connection a pool creates has a distinct identity, an exhausted pool suspends a renter until a connection is returned, a returned connection is handed to the exact renter that was already waiting, a cancelled rent while waiting throws, a disposed lease returns its connection, and a pooled connection exchanges a manifest and gets back an acknowledgement. For `ManifestWriter`: serializing a manifest rents one buffer, returns it cleared, and a buffer returned by one call is the exact buffer a later call reuses, verified by decoding the reused buffer's contents back through `Parse`. Every test is deterministic: waiting on a channel read and asserting `IsCompleted` directly replaces any reliance on a timeout or wall-clock sleep.

## When not to use it

A pool is the wrong tool the moment the resource it manages is not scarce. Pooling a DTO, a small record, or any object cheap enough that the generational garbage collector reclaims it without measurable cost adds a rent call, a return call, and a place to store idle instances, real code and real indirection, for an allocation that was never a problem. Measure first: reach for `GatewayConnectionPool`'s shape, or `Microsoft.Extensions.ObjectPool`, only once an allocation or a connection setup has shown up as a real cost, not because pooling sounds like the disciplined choice.

A pool can also hide a leak rather than prevent one. If a code path rents a connection or a lease and an exception, an early return, or a forgotten `Dispose` skips the return, the pool's `Available` count quietly shrinks and never recovers, and the failure only surfaces later as unrelated-looking timeouts once every connection is checked out and nothing is coming back. The lease pattern closes most of that gap by tying return to scope, but it only helps for code that uses `using`; a caller that holds a bare `IGatewayConnection` from `RentAsync` and skips the `try`/`finally` around `Return` can still leak a connection the same way a `lock` without a `finally` can leak a held monitor.

`ArrayPool<T>` reuse carries its own specific risk: forgetting `clearArray: true` on `Return` does not break any test that only checks the length-trimmed result, because the trimmed output is still correct. It leaves stale bytes sitting in the returned array for whoever rents that backing array next, which is invisible until something else reads past the bounds a well-behaved caller would respect. If the data a buffer carries is sensitive, carrier credentials, consignment contents, anything that should not outlive its own call, clearing on return is not an optional tidiness step.

<blockquote class="ai-sidebar" markdown="1">
An assistant is a reasonable pair for scaffolding a pool or a buffer-reuse routine: wiring a channel-backed rent/return pair, writing the `IAsyncDisposable` cleanup loop, or drafting the `UpperBoundBytes` calculation and the `try`/`finally` around a rented array. What has to stay a human call is whether the resource being pooled is scarce enough to justify any of that machinery in the first place, and whether the return path holds up end to end: every exit path releasing the resource, every `ArrayPool<T>.Return` call clearing the array when the data warrants it, and every lease's `Dispose` wired to the right pool. An assistant asked to "pool this" will happily produce a pool for something that did not need one, and a fast, confident `_pool.Return(scratch)` with the `clearArray` argument quietly defaulted to `false` compiles and passes a test that only checks the output, while leaving the real risk sitting in the array for a reviewer to catch by hand.
</blockquote>

## For deeper coverage

- Kircher, Jain, [*Pattern-Oriented Software Architecture, Volume 3: Patterns for Resource Management*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+3,+Patterns+for+Resource+Management-p-9780470845257) (Wiley, 2004): the catalogue entry for Resource Pool, Eager Acquisition, and Resource Lease behind `GatewayConnectionPool` and `GatewayLease`.
- Microsoft Learn, [ArrayPool\<T\> Class](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1): the authority for `Rent`, `Return`, and the `Shared` instance that `ManifestWriter`'s tests replace with a dedicated pool instead.
- Microsoft Learn, [Object reuse with ObjectPool in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool): the reference for `Microsoft.Extensions.ObjectPool`'s `Get`/`Return` shape and `PooledObjectPolicy<T>`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
