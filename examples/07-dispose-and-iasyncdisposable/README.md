# 07-dispose-and-iasyncdisposable

Deterministic cleanup with `IDisposable` and `IAsyncDisposable`: a
`ManifestWriter` that owns a synchronous `FileStream` handle, and a
`LedgerAppender` that owns an async-capable one, each released the instant a
`using` or `await using` scope ends rather than whenever the garbage
collector happens to finalize the object.

**What the tests prove.** `ManifestWriterTests` opens the same manifest file
a second time with `FileShare.None` while a `ManifestWriter` is still alive
inside its `using` scope and shows that second open fails, then shows it
succeeds the moment the scope exits: the handle is released deterministically
at `Dispose`, not at GC. A separate test calls `WriteLine` after `Dispose` and
asserts it throws `ObjectDisposedException`, and another calls `Dispose`
twice to show the guard makes that a no-op. `LedgerAppenderTests` does the
same for `await using` and `DisposeAsync`: the ledger file is locked while the
appender is alive, unlocked and holding the flushed line the instant the
`await using` scope exits, throws `ObjectDisposedException` on use after
`DisposeAsync`, and tolerates a second `DisposeAsync` call.

Shipping domain, no containers: `ManifestWriter` appends consignment manifest
lines (shipment, destination, weight) to a file; `LedgerAppender` appends
booking-ledger lines to a second file, asynchronously.

`ManifestWriter` is sealed and owns only one managed `IDisposable`, so it uses
the simple one-method `Dispose()` form with no finalizer and no
`Dispose(bool)` overload: the full dispose pattern exists for types that can
be subclassed or that own unmanaged resources directly, and neither applies
here.

## Build and test

```sh
cd examples/07-dispose-and-iasyncdisposable
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
