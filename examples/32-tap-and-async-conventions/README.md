# 32-tap-and-async-conventions

The Task-based Asynchronous Pattern (TAP) naming and signature conventions,
applied to a carrier dispatch gateway.

- **`ICarrierGateway`.** `DispatchAsync` hands a single `DispatchRequest` to
  the carrier and returns a `Task<DispatchReceipt>`. `DispatchBatchAsync`
  dispatches a batch of requests in order and returns a
  `Task<IReadOnlyList<DispatchReceipt>>`, optionally reporting a
  `DispatchProgress` snapshot to an `IProgress<DispatchProgress>` after each
  item. `IsReachableAsync` returns a `ValueTask<bool>`: a reachability check
  often has an answer ready without ever going asynchronous, and a
  `ValueTask<bool>` reports it without allocating a `Task<bool>` for work that
  never actually ran. Every member ends in "Async", and every one accepts a
  trailing `CancellationToken` so a caller can cancel a dispatch instead of
  the gateway inventing its own cancellation story.
- **`FakeCarrierGateway`.** An in-memory `ICarrierGateway` for tests and
  demos. It never reads the wall clock: the `DateTimeOffset` on every receipt
  comes from the value given to its constructor (or `DateTimeOffset.UnixEpoch`
  if none is given), so a test can assert on the exact receipt it gets back.
  Every library-side `await` is configured with `.ConfigureAwait(false)`.

**What the tests prove.** `DispatchAsyncTests` dispatches a single request and
checks the returned receipt, then cancels a token up front and asserts
`DispatchAsync` honours it. `BatchProgressTests` dispatches a batch and
asserts a hand-rolled, synchronous `RecordingProgress` double receives one
report per request with a monotonically increasing `Completed` count, that a
null progress callback is tolerated, that cancelling partway through a batch
stops it before every request is dispatched, and that the returned receipts
preserve request order. `AsyncConventionTests` asserts
`IsReachableAsync`'s `ValueTask<bool>` is already `IsCompleted` by the time
the call returns, then uses reflection over `ICarrierGateway` to prove every
`Task`/`Task<T>`/`ValueTask`/`ValueTask<T>`-returning member is named with the
"Async" suffix and accepts a `CancellationToken`.

`RecordingProgress` exists because the built-in `System.Threading.Progress<T>`
marshals its callback onto a captured `SynchronizationContext` asynchronously,
which would race against these tests' assertions; `RecordingProgress.Report`
appends to its list on the calling thread, immediately.

Shipping domain, no containers, pure BCL: no runtime package is needed to
demonstrate the TAP conventions.

## Build and test

```sh
cd examples/32-tap-and-async-conventions
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
