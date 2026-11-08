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

By the end of this chapter you can:

- Build a fixed-size async connection pool over a `Channel<T>` that is both the idle store and the wait queue.
- Explain why an exhausted pool suspending `RentAsync` is correct backpressure, and which waiting caller a returned connection goes to.
- Wrap a borrowed resource in a lease so a `using` block guarantees return even when the caller throws.
- Rent, use and return a scratch buffer through `ArrayPool<byte>`, and say why `clearArray: true` matters for carrier or EDI data.
- Judge when array-pool reuse is worth it against allocating a fresh array.

## Why this matters

Most objects are cheap. Allocating a `List<T>`, a DTO or a small record costs a few nanoseconds, and the garbage collector reclaims it unnoticed. Pooling those costs more in rent, return and idle storage than it saves. Pooling pays only when a resource is expensive to create, limited in count, or large enough that repeated allocation shows up as garbage-collector pressure.

A carrier gateway connection is scarce in the first two senses. Opening one means a handshake and authentication with a remote system, and the carrier's infrastructure or contract usually caps how many connections a shipper holds open. A large scratch buffer for serializing manifests is scarce in the third: unlimited in count, but costly enough per allocation, on a hot path, to cause visible churn.

Both share one shape: acquire, use, give back, with giving back automatic enough that a caller can't forget. *Pattern-Oriented Software Architecture, Volume 3* catalogs the pieces: Resource Pool, Eager Acquisition and Resource Lease. The runtime supplies most of the code. `Channel<T>` already suspends a reader until a writer arrives, so a connection pool needs no hand-rolled wait queue, and `ArrayPool<T>` already does bucketed array reuse.

{% include excalidraw.html name="37-pools-object-connection-arraypool" caption="Figure 37.1: A fixed-size connection pool hands out connections and takes them back on return; a scratch buffer pool reuses memory the same way." %}

## Pattern: Resource Pool

### The problem

Opening a carrier connection per manifest repeats an expensive handshake, and opening as many as demand asks for exceeds what the carrier allows.

### The solution

**A Resource Pool holds a bounded set of reusable instances that callers rent and return, instead of creating and destroying one per use.**

### How C# models it

`GatewayConnectionPool` calls its factory exactly `size` times in the constructor and writes every connection into an unbounded `Channel<IGatewayConnection>`. That's Eager Acquisition: the full membership exists when construction completes. `Size` is the fixed count; `Available` is the channel reader's `Count`.

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

The one channel does two jobs: it stores idle connections, and it's the wait queue for callers who arrive when none are idle. `RentAsync` is a channel read, which completes at once if an item is there and suspends otherwise. `Return` is a channel write, which restores the connection to the idle set or completes a parked reader's read directly. Renting all three connections from a pool of three yields three distinct `Id` values.

### Exhaustion is backpressure, not a bug

A pool smaller than demand runs out by design. A renter arriving at `Available == 0` gets no exception, no null and no busy-wait: it gets an incomplete `ValueTask`, parked on the same channel read, until a `Return` elsewhere frees a connection.

{% include excalidraw.html name="37-connection-pool-exhaustion" caption="Figure 37.2: A two-connection pool leaves a third rent incomplete until a connection comes back, and that exact connection goes to the caller who was already waiting." %}

With both connections of a two-connection pool rented:

- a third `RentAsync` reports `IsCompleted` as `false`, parked, not slow;
- returning one connection completes it, and the waiter receives that exact instance;
- cancelling a parked rent through its token throws `OperationCanceledException`, so a caller who gives up isn't stranded.

If the pool grew to meet demand, or threw when empty, the fixed size would mean nothing, and neither would the carrier's cap it protects. Waiting tells the caller the system is at capacity; the right response is to wait, not to force a connection the far end wasn't provisioned for.

### Object pools beyond a single connection type

For plain objects that are only expensive to construct, `Microsoft.Extensions.ObjectPool` is .NET's answer. `ObjectPool<T>` has a synchronous `Get`/`Return` pair backed by a `PooledObjectPolicy<T>` that creates and resets objects, and ASP.NET Core uses it to recycle `StringBuilder` instances.

It limits how many objects it retains, not how many it hands out. `Get` on an empty pool constructs a fresh instance instead of waiting:

| | `ObjectPool<T>` | `GatewayConnectionPool` |
| --- | --- | --- |
| Rent when empty | Constructs a new instance | Suspends until a return |
| Hard cap on live instances | No | Yes |
| API | Synchronous `Get`/`Return` | `RentAsync`/`Return` |
| Fits | `StringBuilder` and similar | A rationed resource such as carrier connections |

### Reusing buffers with an array pool

`ManifestWriter` serializes `ManifestLine` items into the gateway's wire format, `Code=Value` per line in UTF-8, without allocating a fresh scratch array per call:

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

`UpperBoundBytes` computes a deterministic worst case: the maximum UTF-8 expansion of every code and value, plus one byte per `=` and `\n`. `_pool` is a constructor-injected `ArrayPool<byte>`, not a hard-coded `ArrayPool<byte>.Shared`, so a recording pool can stand in and observe rents and returns without the shared pool's process-wide, non-deterministic buckets. Only the final `ToArray()` allocates, trimmed to the bytes written. The `finally` returns the buffer even if encoding throws.

{% include excalidraw.html name="37-arraypool-buffer-reuse" caption="Figure 37.3: Renting a buffer, using it, and returning it cleared means the next rent gets back the same array instead of a fresh allocation." %}

A recording wrapper over a dedicated `ArrayPool<byte>.Create()` instance shows the reuse: serializing the same manifest twice rents, for the second call, the exact array returned by the first, and both calls produce identical bytes. `ManifestWriter.Parse`, the inverse of `Serialize`, decodes the output back to the original lines.

### Why clearArray matters for carrier data

`ArrayPool<T>.Return` clears only when asked, and `ManifestWriter` always passes `clearArray: true`. Without it, a returned array keeps the last manifest's bytes until the next `Rent` hands that array to another caller. If the next manifest is shorter, the tail past `written` still holds the previous carrier name, consignment weight or destination.

That tail can't leak through `Serialize`, whose result is trimmed. The risk is any other renter of the same array that reads past the bounds a correct caller respects, through a bug, a cast, or code that treats the full rented length as data. Clearing on return means every rented array starts zeroed, so one manifest's freight data has no path into memory another's serialization might expose. Clearing is opt-in, so a check that the last return cleared guards it; a refactor that dropped the argument would otherwise still produce correct output.

### When buffer reuse pays off versus allocating fresh

Array-pool reuse pays when all three hold:

- the array is large enough that allocating and discarding it shows up as garbage-collector pressure;
- the path runs often enough for that pressure to matter;
- the buffer's lifetime is short and scoped to one call.

`ManifestWriter.Serialize` meets all three: it runs on every outbound manifest, its buffer lives for one call, and similarly sized manifests rent from the same bucket and get the same array back.

It stops paying when:

- **a buffer is held for an object's lifetime**, so nothing else can reuse it and the pool is a slower allocator;
- **sizes vary widely between calls**, spreading rentals across many buckets instead of concentrating reuse;
- **arrays are small and short-lived**, a few dozen bytes, which the generational collector already reclaims cheaply.

## Pattern: Resource Lease

### The problem

`RentAsync` leaves return to the caller, on every path, including the ones that throw. A missed `Return` shrinks the pool for good.

### The solution

**A Resource Lease ties a borrowed resource to a scoped value whose disposal returns it.**

### How C# models it

`GatewayLease` is a `readonly record struct` holding the pool and the connection; its only behavior is `Dispose`:

```csharp
public readonly record struct GatewayLease(GatewayConnectionPool Pool, IGatewayConnection Connection)
    : IDisposable
{
    public void Dispose() => Pool.Return(Connection);
}
```

`RentLeaseAsync` is `new(this, await RentAsync(ct))`. With `using var lease = await pool.RentLeaseAsync(ct);`, the connection is `lease.Connection`, and when the scope ends, normally or by exception, `Dispose` returns it. A one-connection pool shows `Available` as 0 inside the block and 1 after it.

A record struct fits: a lease is compared by what it holds, and it adds no heap allocation beyond the connection, just a pairing of two references. Forgetting to return a connection stops being a bug a caller can write by accident, short of discarding the lease without disposing it.

## Build, run, observe

```
cd examples/37-pools-object-connection-arraypool && dotnet test
```

All 9 tests pass. They cover distinct connection identities, suspension on an exhausted pool, hand-off of the returned connection to the waiting renter, cancellation of a parked rent, return on lease disposal, a manifest exchange over a pooled connection, and for `ManifestWriter`, one rent per call, a cleared return, and buffer reuse verified through `Parse`. Asserting `IsCompleted` on a channel read replaces timeouts and sleeps.

## When not to use it

- **The resource isn't scarce.** Pooling a DTO or a small record adds a rent, a return and idle storage for an allocation that was never a problem. Measure first; reach for `GatewayConnectionPool`'s shape or `Microsoft.Extensions.ObjectPool` only after allocation or connection setup shows up as a real cost.
- **Return can be skipped.** An exception, an early return or a forgotten `Dispose` shrinks `Available` permanently, and the failure surfaces later as unrelated-looking timeouts once every connection is out. A lease helps only where callers use `using`; a bare `RentAsync` without `try`/`finally` around `Return` leaks the same way a `lock` without a `finally` would.
- **`clearArray` is forgotten.** Output stays correct, so nothing that checks the trimmed result notices, and stale bytes wait for the next renter. For credentials, consignment contents or anything that shouldn't outlive its call, clearing is required.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a pool.** "Pool the gateway connections" leaves open what an empty pool does, how a connection gets back and whether a reused buffer is cleared. State them:

> In `GatewayConnectionPool`, hold connections in a `Channel<IGatewayConnection>` so `RentAsync` waits on an exhausted pool and honors its token. `RentLeaseAsync` returns a `GatewayLease` whose `Dispose` calls `Return`, and callers take it with `using`. In `ManifestWriter.Serialize`, rent `UpperBoundBytes(lines)` bytes from the injected `ArrayPool<byte>`, copy the written span out with `ToArray()`, and call `_pool.Return(scratch, clearArray: true)` in a `finally`. Don't pool `ManifestLine` or any other small record.

Run `dotnet test --project GatewayPooling.Tests --filter "ConnectionPoolTests|ManifestBufferTests"`; all 9 must pass.
</blockquote>

## For deeper coverage

- Kircher, Jain, [*Pattern-Oriented Software Architecture, Volume 3: Patterns for Resource Management*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+3,+Patterns+for+Resource+Management-p-9780470845257) (Wiley, 2004): the catalogue entry for Resource Pool, Eager Acquisition, and Resource Lease behind `GatewayConnectionPool` and `GatewayLease`.
- Microsoft Learn, [ArrayPool\<T\> Class](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1): the authority for `Rent`, `Return`, and the `Shared` instance that `ManifestWriter`'s tests replace with a dedicated pool instead.
- Microsoft Learn, [Object reuse with ObjectPool in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool): the reference for `Microsoft.Extensions.ObjectPool`'s `Get`/`Return` shape and `PooledObjectPolicy<T>`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
