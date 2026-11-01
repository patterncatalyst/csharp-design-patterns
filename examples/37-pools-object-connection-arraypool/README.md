# 37-pools-object-connection-arraypool

Two pooling strategies over the same idea: reusing an expensive-to-create or
expensive-to-allocate resource instead of creating or allocating one every
time.

- **Connection pool.** `GatewayConnectionPool` holds a fixed set of
  `IGatewayConnection` instances in a `System.Threading.Channels.Channel<T>`.
  The channel is both the storage for idle connections and the wait queue for
  an exhausted pool: `RentAsync` reads from the channel, and reading from an
  empty channel suspends the caller until `Return` writes a connection back.
  No polling, no semaphore, no wall-clock timeout. `GatewayLease` is a
  `readonly record struct` wrapping a rented connection; disposing the lease
  returns the connection automatically, so a `using` block guarantees return
  even if the caller throws.
- **ArrayPool buffer reuse.** `ManifestWriter.Serialize` encodes a list of
  `ManifestLine` records into the wire format (`Code=Value\n`, UTF-8) without
  allocating a fresh scratch array per call. It computes a deterministic
  upper bound on the encoded byte count from the input, rents a buffer at
  least that large from an `ArrayPool<byte>`, encodes into it, copies out
  only the exact-length result, and returns the scratch buffer to the pool in
  a `finally` block. `ManifestWriter.Parse` is the inverse, decoding the same
  format back into `ManifestLine` records.

**What the tests prove.** For the connection pool: a fresh pool hands out a
connection with a valid identity and decrements `Available`; renting every
connection produces distinct identities; renting past the pool's size
suspends the caller, which `!pendingRent.IsCompleted` demonstrates against a
known-empty pool rather than racing a timeout, and returning a connection
unblocks that pending rent with the exact connection that was returned; a
pending rent's own cancellation token is honored; disposing a `GatewayLease`
returns its connection; and a rented connection's `ExchangeAsync` behaves
like a real round trip. For the ArrayPool buffer: serializing a manifest
rents exactly one buffer and returns it exactly once, cleared
(`clearArray: true`, since a borrowed buffer may carry over a previous
manifest's bytes); and serializing the same input twice reuses the same
underlying array on the second rent, which `RecordingArrayPool` observes by
wrapping a dedicated `ArrayPool<byte>.Create()` instance (never
`ArrayPool<byte>.Shared`, whose buckets are shared process-wide and are not
deterministic across test runs) and recording every rent and return.
`ManifestWriter.Parse` is then used to prove the round trip is lossless.

No `Thread.Sleep`, no `Task.Delay`, and no wall-clock reads anywhere in this
example, in the library or the tests: `GatewayConnection.ExchangeAsync` uses
`Task.Yield()` for its asynchronous continuation, and pool exhaustion is
proven by asserting `!pendingRent.IsCompleted` on a pool with a known,
empty-of-idle-connections size, rather than racing a timeout.

## Build and test

```sh
cd examples/37-pools-object-connection-arraypool
dotnet test --solution GatewayPooling.slnx
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
