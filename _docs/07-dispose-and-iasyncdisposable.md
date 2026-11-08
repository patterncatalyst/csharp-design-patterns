---
title: "The Dispose pattern and IAsyncDisposable"
order: 7
layout: tutorial
sectionid: docs
part: "dotnet-substrate"
description: "Dispose(bool), finalizer interaction, using and await using, and the closest C# gets to RAII, specified by the Framework Design Guidelines."
---

<!-- EXAMPLE (shipping domain): A ManifestWriter holding a file handle, and an async ledger append. -->

## Learning objectives

By the end of this chapter you can:

- State what `IDisposable` and `using` guarantee about *when* a resource is
  released, and how that differs from garbage collection.
- Implement the idempotency guard, a `_disposed` field checked with
  `ObjectDisposedException.ThrowIf`, and say why a sealed class owning only
  managed `IDisposable` members needs no finalizer and no `Dispose(bool)`.
- Implement `IAsyncDisposable` with a `ValueTask`-returning `DisposeAsync`, and
  say why an asynchronous flush can't run inside a synchronous `Dispose`.
- Apply `ConfigureAwait(false)` on every `await` in a library's async dispose
  path.
- Choose the half of the dispose pattern a type needs from what it owns.

## Why this matters

`ManifestWriter` opens a consignment manifest with `FileShare.None`, so nobody
else sees or overwrites a half-written line. The exclusivity is right; its
duration is the problem. If the handle is released only when the garbage
collector finalizes the abandoned writer, the lock lasts as long as the
collector decides: seconds under memory pressure, much longer with none. A
reconciliation job, a person checking a shipment or a scheduled audit sweep is
refused for an interval nobody can predict from the source.

`LedgerAppender` buffers before it writes. An unflushed `StreamWriter` waiting
for finalization holds bytes describing a booking that, on disk, never
happened. A crash, a recycle or a forgotten close leaves the file short of what
the application believes it recorded. The one guaranteed moment to close that
gap is the end of a `using` or `await using` scope, which runs before control
leaves the scope.

{% include excalidraw.html name="07-dispose-and-iasyncdisposable" caption="Figure 7.1: A using scope releases a file handle the instant it exits, not at an unpredictable garbage-collection pass; an await using scope awaits an asynchronous flush before unlocking the file it wrote to." %}

## `using` and the scope that releases a handle on schedule

A type that holds a file handle, socket or lock implements `IDisposable` and
frees the resource when `Dispose()` is called, "when the instance that
implements `IDisposable` is no longer needed"
([`IDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable)).
The interface can't force a caller to call it. The
[`using` statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using)
can: it calls `Dispose` "even if an exception occurs", through a
compiler-generated `try`/`finally`. A `using` declaration, the form without
braces that ends at the enclosing scope, compiles to the same `try`/`finally`.

While a `ManifestWriter` is alive inside a `using` block, a second open of the
same path with `FileShare.None` throws `IOException`. The moment the block ends,
the same open succeeds, with no delay, retry or `GC.Collect()`. The resource's
lifetime is a lexical scope you can see on the page. The collector "does not
allocate or release unmanaged memory"
([Cleaning up unmanaged resources](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/unmanaged)),
and it promises no time for running a finalizer. Only a `using` scope promises an
instant.

## The dispose pattern: a guard, not a ceremony

Objects get disposed twice: an error path calls `Dispose` and then the `using`
scope ends, or two collaborators both close a shared resource. `ManifestWriter`
guards with a `_disposed` field:

```csharp
public void Dispose()
{
    if (_disposed)
    {
        return;
    }

    _stream.Dispose();
    _disposed = true;
}
```

The first call releases the `FileStream` and sets the field; later calls do
nothing. Disposing twice throws nothing, and the earlier write survives. The
same field refuses use after disposal, with one line at the top of every public
member that touches the resource:

```csharp
public void WriteLine(ManifestEntry entry)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    // ...
}
```

`ObjectDisposedException.ThrowIf` is the BCL helper for this guard, so it reads
the same everywhere and a reviewer recognizes it at once. Writing to a disposed
writer throws `ObjectDisposedException`. A boolean field and one guard call per
entry point are the whole pattern for this type, whatever resource sits behind
it: a `FileStream`, a socket or a lock.

## Why `ManifestWriter` needs no finalizer and no `Dispose(bool)`

The commonly shown dispose pattern is longer: a protected
`Dispose(bool disposing)`, a public `Dispose()` that calls it with `true` and
then `GC.SuppressFinalize(this)`, and a finalizer that calls it with `false`.
[Implement a `Dispose` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
gives that shape for "a class that holds unmanaged resources directly" or one
that can be subclassed, where the finalizer is the backstop if nobody calls
`Dispose`.

`ManifestWriter` is neither:

- It's `sealed`, so no subclass can add an unmanaged resource beneath it.
- Its one resource is a `FileStream`, a managed wrapper with its own finalizer.

A second finalizer around an already-finalizable `FileStream` adds no safety. It
adds an object the collector must promote to a later generation and finalize
before reclaiming its memory.
[CA1063](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1063),
"Implement `IDisposable` correctly," and
[CA1816](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1816),
"Call `GC.SuppressFinalize` correctly," flag a type that takes the full pattern
without an unmanaged resource or a finalizer of its own to justify it.

{% include excalidraw.html name="07-dispose-pattern-paths" caption="Figure 7.2: Three dispose shapes for three different ownership cases: a sealed type with only managed resources needs just a guarded Dispose; one holding an unmanaged handle directly needs the full finalizer pattern; one whose release must await a flush implements the asynchronous contract instead." %}

The one-method `Dispose()` is the right amount of pattern for a sealed class with
one managed `IDisposable` field. Use the full `Dispose(bool)` and finalizer shape
when a type holds an unmanaged resource directly or must support subclassing.

## `IAsyncDisposable`, `await using`, and a `ValueTask` return

`Dispose()` returns `void`, so it can't `await`. It can block on the result,
risking a deadlock on the caller's synchronization context, or start the work
and return before it finishes, which defeats deterministic release.
`LedgerAppender` must flush its last write and release its handle before the
caller can assume the ledger is durable, and flushing a `StreamWriter` over an
asynchronous `FileStream` is itself asynchronous.

[`IAsyncDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable)
provides `DisposeAsync` for freeing resources "*asynchronously*".
[Implement a `DisposeAsync` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync)
returns `ValueTask`, not `Task`, because a disposal that often completes
synchronously, with nothing to flush, shouldn't allocate a `Task` every time.

`LedgerAppender.DisposeAsync` uses the same guard, then awaits the one thing it
owns:

```csharp
public async ValueTask DisposeAsync()
{
    if (_disposed)
    {
        return;
    }

    await _writer.DisposeAsync().ConfigureAwait(false);
    _disposed = true;
}
```

Disposing a `StreamWriter` asynchronously flushes its buffer to the
`FileStream` before closing it. When the `await` completes, the ledger line is
on disk and the file is unlocked. `await using` compiles to a `try`/`finally`
that awaits `DisposeAsync` in the `finally` block. The file stays locked for the
whole scope, and the moment the scope ends the appended line is readable and the
file is free.

## `ConfigureAwait(false)` on the way out

Every `await` in `DisposeAsync` carries `.ConfigureAwait(false)`. Without it, the
continuation tries to resume on the `SynchronizationContext` or `TaskScheduler`
that was current when the `await` began, which a UI framework needs to get back
to its UI thread. A library has no UI thread and no business assuming a caller's
context. Resuming there costs scheduling overhead at best, and at worst
deadlocks when that context is single-threaded and busy waiting on the same
call.

[Implement a `DisposeAsync` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync)
uses the same idiom, awaiting an inner `DisposeAsync` with
`ConfigureAwait(false)` before setting a disposed flag. Per
[`Task.ConfigureAwait`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait),
`false` means the continuation doesn't need to run on the original context,
which is true of the single field assignment after the `await`. Apply it on
every `await` in every async method a library exposes, not only the ones you
remember, or the bug surfaces when a future caller runs the library from a
context that matters.

## `ManifestWriter` and `LedgerAppender` side by side

| | `ManifestWriter` | `LedgerAppender` |
|---|---|---|
| Owns | One `FileStream` | One `StreamWriter` over a `FileStream` |
| Opened with | `FileShare.None` | `FileShare.None` and `FileOptions.Asynchronous` |
| Writes | Synchronously: small, infrequent lines where blocking for a local disk write costs nothing | Asynchronously: a hotter write path, where an async-capable handle pays off |
| Releases with | `IDisposable.Dispose()` | `IAsyncDisposable.DisposeAsync()` |
| Sealed, `_disposed` guard, `ThrowIf` on every public member | Yes | Yes |
| Finalizer or `Dispose(bool)` | No | No |

`FileOptions.Asynchronous` has to be set for reads and writes on that handle to
run asynchronously, instead of falling back to a blocking call inside a `Task`.

One question decides the interface: does releasing the resource require awaiting
something? `ManifestWriter`'s `Stream.Dispose()` closes an already-flushed,
synchronously written stream, which is synchronous work. When a release has to
await a flush, as `LedgerAppender`'s does, `IAsyncDisposable` is the only
contract that expresses it without blocking a thread.

## Build, run, observe

```sh
cd examples/07-dispose-and-iasyncdisposable
dotnet test
```

All 7 tests pass; `./scripts/verify.sh` runs the same suite. For both types, the
file is locked to a second exclusive opener for the whole scope and free the
moment it exits, use after disposal throws `ObjectDisposedException`, and a
second dispose is harmless. After an `await using` scope ends, the ledger file
contains the appended line. Every check reads the file system or the exception
type.

## When not to use it

- **A type that owns nothing releasable.** `ManifestEntry`, a `readonly record
  struct` with a shipment ID, a destination and a weight, gets no `Dispose`. One
  would do nothing and put an empty `using` at every call site.
- **A wrapper around one disposable.** A method that opens a `ManifestWriter`,
  writes a few lines and returns needs a `using` around that call, not a second
  disposable type to manage the first. A wrapper earns its cost only when it adds
  cleanup of its own: a second resource, a counter that needs flushing, state
  that outlives one call.
- **A finalizer "to be safe".** On a type with no unmanaged resource and no
  subclass that could add one, the finalizer runs for every instance that
  reaches the finalization queue, slowing collection for a guarantee the type
  never needed.

Implement the one interface that matches whether release needs an `await`,
guard it with one boolean field, and add more only when an unmanaged handle or a
subclassing requirement appears.

> **🤖 Explore with your agent**
>
> "`ManifestWriter` is sealed and owns one `FileStream`: implement `IDisposable` with a single `Dispose()` that disposes the stream and sets `_disposed`, with no finalizer, no `Dispose(bool)` and no `GC.SuppressFinalize`. `LedgerAppender` writes through a `StreamWriter` asynchronously: implement `IAsyncDisposable` with a `DisposeAsync()` that returns `ValueTask` and awaits `_writer.DisposeAsync()`, never `.GetAwaiter().GetResult()`. In both, a second dispose is a no-op, and `WriteLine` or `AppendAsync` after disposal throws through `ObjectDisposedException.ThrowIf`."
>
> Check with `dotnet test --project ManifestWriting.Tests --filter Dispos`; a `~ManifestWriter` finalizer in the reply is the pattern this class doesn't need.

## For deeper coverage

- [Implement a `Dispose` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose) and [Implement a `DisposeAsync` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync) on Microsoft Learn: the canonical shape of the synchronous and asynchronous dispose patterns, including the `ConfigureAwait(false)` idiom used here.
- The [`IDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable) and [`IAsyncDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface references, the [`using` statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using) language reference, and [Cleaning up unmanaged resources](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/unmanaged): the primary sources for every claim about scope-exit timing versus garbage collection.
- [CA1063: Implement `IDisposable` correctly](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1063) and [CA1816: Call `GC.SuppressFinalize` correctly](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1816): the analyzer rules behind the claim that a sealed class owning only managed disposables should not carry a finalizer.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
