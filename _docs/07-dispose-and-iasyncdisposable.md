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

- State what `IDisposable` and the `using` statement guarantee about *when*
  a resource is released, and explain why that timing is fundamentally
  different from when the garbage collector would reclaim the same object.
- Implement the dispose pattern's idempotency guard, a `_disposed` field
  checked with `ObjectDisposedException.ThrowIf`, and explain why a sealed
  class that owns only managed `IDisposable` members needs neither a
  finalizer nor a `Dispose(bool)` overload.
- Implement `IAsyncDisposable` with a `DisposeAsync` method that returns
  `ValueTask`, and explain why an asynchronous flush belongs there and
  cannot run inside a synchronous `Dispose`.
- Apply `ConfigureAwait(false)` correctly on every `await` inside a
  library's async dispose path, and say what it buys and what it would cost
  to skip.
- Trace how `ManifestWriter`'s synchronous `FileStream` and
  `LedgerAppender`'s asynchronous `StreamWriter` each apply the correct half
  of the dispose pattern for exactly what they own.

## Why this matters

A consignment manifest is a file a warehouse keeps open only as long as it
is actively writing new shipment lines into it. `ManifestWriter` opens that
file exclusively, `FileShare.None`, for the same reason a loading-dock clerk
locks a paper logbook while filling in an entry: nobody else should see a
half-written line, and nobody else should be able to overwrite it mid-write
either. The exclusivity itself is correct. The problem is how long it lasts.
If the handle backing `ManifestWriter` only lets go when the garbage
collector eventually finalizes the abandoned object, "how long it lasts"
stops being a line of code anyone wrote and becomes whatever the collector
decides: a few seconds under light memory pressure, much longer under none
at all. A second process that wants that same file, a reconciliation job, a
human checking a shipment, an audit sweep running on a schedule, gets
refused for an interval nobody scheduled and nobody can predict by reading
the source.

`LedgerAppender` raises the same problem with worse stakes, because it
buffers before it writes. An unflushed `StreamWriter` sitting on a
finalizer queue is not just locking a file somebody else wants to read; it
is holding bytes that describe a booking that, as far as the ledger file on
disk is concerned, never happened. A process crash, a container recycle, or
code that simply forgot to close the appender before moving on leaves a gap
between what the application believes it recorded and what the file
actually contains. .NET gives a program exactly one guaranteed moment to
close that gap on purpose: the end of a `using` or `await using` scope,
which runs before control leaves that scope, not whenever a future
collection happens to run a finalizer. Everything in this chapter, the
dispose pattern's guard, the split between synchronous and asynchronous
cleanup, exists to make that one guaranteed moment do the right thing every
time it runs.

{% include excalidraw.html name="07-dispose-and-iasyncdisposable" caption="Figure 7.1: a using scope around ManifestWriter holds its FileStream handle exclusively and releases it the instant the scope exits, not at an unpredictable GC pass; an await using scope around LedgerAppender awaits DisposeAsync to flush and unlock the ledger file before the scope completes." %}

## `using` and the scope that releases a handle on schedule

`IDisposable` is a one-method contract: a type that holds something worth
releasing deterministically, a file handle, a network socket, a lock,
implements `Dispose()` and promises that calling it frees that resource
right then, not eventually. The
[`IDisposable` interface documentation](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable)
describes exactly this: a type implements the interface "to release
unmanaged resources" and callers "call the `Dispose` implementation... when
the instance that implements `IDisposable` is no longer needed." Nothing
about the interface forces a caller to use it correctly; a consumer that
never calls `Dispose` still compiles. What makes the contract reliable in
practice is the `using` statement, specified on the
[`using` statement reference page](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using)
as syntax that "ensures the correct use of `IDisposable` instances" by
calling `Dispose` "even if an exception occurs" inside the block, through a
compiler-generated `try`/`finally`. A `using` declaration, the form without
braces that ties the call to the end of the enclosing scope instead of an
explicit block, compiles to the identical `try`/`finally`; both forms run
`Dispose` at one precise, readable point in the method, not at some later
point the method's author has no control over.

`ManifestWriterTests.Handle_is_released_as_soon_as_the_using_scope_exits`
makes that determinism checkable rather than assumed. While a
`ManifestWriter` is alive inside a `using` block, the test opens the same
path a second time with `FileShare.None` and asserts that the second open
throws `IOException`, because the first handle is still held exclusively.
The instant the `using` block ends, the same open call that just failed
succeeds, with no delay, no retry loop, and no `GC.Collect()` anywhere in
the test. That is the entire value proposition of `IDisposable` plus
`using` over relying on the collector alone: a resource's lifetime is tied
to a lexical scope a reader can see on the page, not to generation
boundaries and finalization queues a reader cannot see at all. The
[cleaning up unmanaged resources documentation](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/unmanaged)
states plainly that ".NET's garbage collector does not allocate or release
unmanaged memory," and more to the point here, it does not promise *when*
it will run a finalizer that happens to release something at all; a `using`
scope is the only mechanism in the language that promises a specific
instant.

## The dispose pattern: a guard, not a ceremony

A `Dispose` method that assumes it only ever runs once is a method waiting
to break the moment a caller disposes an object twice, which happens more
often than it should: a `using` scope ending normally after code earlier in
the method already called `Dispose` explicitly on an error path, or two
collaborators each closing the same shared resource out of defensiveness.
`ManifestWriter` guards against exactly that with a `_disposed` field and an
early return:

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

The guard is what makes `Dispose` idempotent: the first call releases the
`FileStream` and flips the field, every call after that is a no-op.
`ManifestWriterTests.Disposing_twice_is_a_harmless_no_op` calls `Dispose`
twice back to back and then reads the file to confirm the earlier write
survived; nothing throws, nothing double-releases a handle the runtime has
already reclaimed. The same field backs the other half of the contract,
refusing use after disposal, with one line at the top of every public
member that touches the underlying resource:

```csharp
public void WriteLine(ManifestEntry entry)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    // ...
}
```

`ObjectDisposedException.ThrowIf` is a static helper, not hand-rolled `if`
plus `throw`, and it exists specifically for this check: a guard clause
every disposable member needs, written the same way everywhere it appears
so a reviewer recognizes the pattern instantly instead of re-reading a
bespoke conditional each time. `ManifestWriterTests.Writing_after_dispose_throws_object_disposed_exception`
disposes a writer and then calls `WriteLine` on it, asserting the exact
exception type the guard throws. Two small pieces, a boolean field and one
guard call repeated at every entry point, are the entire dispose pattern a
type like this needs. Nothing about either piece depends on what kind of
resource sits behind it; the same shape works whether the field underneath
is a `FileStream`, a socket, or a lock.

## Why `ManifestWriter` needs no finalizer and no `Dispose(bool)`

Books and reference pages about the dispose pattern usually show a longer
version than the one above: a protected `Dispose(bool disposing)` overload,
a public parameterless `Dispose()` that calls it with `true` and then calls
`GC.SuppressFinalize(this)`, and a finalizer that calls the same protected
overload with `false`. The
[Implement a `Dispose` method documentation](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
explains exactly which problem that longer shape solves: it exists for "a
class that holds unmanaged resources directly" or that can be subclassed,
where a finalizer is the last backstop against a resource leaking forever
if some caller, somewhere, never calls `Dispose` at all. `ManifestWriter`
is neither of those things. It is `sealed`, so no subclass can ever add an
unmanaged resource underneath it that this class's `Dispose` would not know
about, and the one resource it owns is a `FileStream`, a managed wrapper
that already carries its own finalizer as the backstop the Microsoft Learn
guidance describes. Wrapping an already-finalizable `FileStream` inside
another type with its own finalizer does not add a second layer of safety;
it adds a second object that the collector has to promote to a later
generation and finalize before its memory can be reclaimed, for a resource
that was already going to be cleaned up by the `FileStream`'s own
finalizer if `Dispose` were somehow never called. That specific
anti-pattern has a name in the analyzer rules the base class library ships
with:
[CA1063](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1063),
"Implement `IDisposable` correctly," and
[CA1816](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1816),
"Call `GC.SuppressFinalize` correctly," both flag exactly this shape when a
type reaches for the full pattern without an unmanaged resource or a
finalizer of its own to justify it.

{% include excalidraw.html name="07-dispose-pattern-paths" caption="Figure 7.2: three dispose shapes side by side. A sealed, managed-only type needs only a guarded Dispose(); an unsealed type that owns an unmanaged handle directly takes the heavier Dispose(bool), finalizer, and GC.SuppressFinalize(this) combination; a type whose release has to await a flush implements IAsyncDisposable instead. ManifestWriter takes the first path and LedgerAppender takes the third." %}

The simple, one-method `Dispose()` this chapter already showed is not a
shortcut taken because the full pattern was too much trouble to write. It
is the correct amount of pattern for what `ManifestWriter` actually owns: a
sealed class, one managed `IDisposable` field, no unmanaged handle it
touches directly. The full `Dispose(bool)` and finalizer shape stays
available in the base class library documentation for the day a type in
this codebase does hold an unmanaged resource directly or needs to support
subclassing safely; reaching for it here, on a type where neither condition
holds, would add a finalizer with nothing underneath it for that finalizer
to protect.

## `IAsyncDisposable`, `await using`, and a `ValueTask` return

A synchronous `Dispose()` method has a signature that returns `void`, which
means it cannot `await` anything inside it without either blocking the
calling thread on the result (risking a deadlock on whatever
synchronization context the caller is running under) or firing work off
and returning before that work finishes, defeating the entire point of a
deterministic release. `LedgerAppender` needs its last write flushed and
its file handle released before the method that disposes it can safely
assume the ledger is durable, and flushing a `StreamWriter` over an
asynchronous `FileStream` is itself an async operation. `IAsyncDisposable`
exists for precisely this case: the
[`IAsyncDisposable` interface documentation](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable)
describes `DisposeAsync` as a method that "performs tasks associated with
freeing, releasing, or resetting unmanaged resources *asynchronously*," and
the
[Implement a `DisposeAsync` method documentation](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync)
is explicit that the method's return type is `ValueTask`, not `Task`,
because a disposal that frequently completes synchronously (nothing left to
flush, the writer was never used) should not pay for a heap-allocated
`Task` every time it runs.

`LedgerAppender.DisposeAsync` applies the same idempotency guard as the
synchronous version, then awaits the one thing it owns:

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

Disposing a `StreamWriter` asynchronously flushes its buffered bytes to the
underlying `FileStream` before closing it, so by the time this method's
`await` completes, the ledger line written earlier is durable on disk and
the file is unlocked. The caller-side mechanics mirror `using` exactly: the
`await using` statement, covered on the same
[`using` statement reference page](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using)
cited earlier, compiles to a `try`/`finally` that `await`s `DisposeAsync`
in the `finally` block instead of calling `Dispose` synchronously.
`LedgerAppenderTests.Ledger_file_is_locked_until_the_await_using_scope_exits`
proves the lock holds for the whole scope the same way the synchronous test
did for `ManifestWriter`, and
`LedgerAppenderTests.Await_using_flushes_and_closes_the_ledger_file` proves
the written line is actually readable, and the file actually unlocked, the
moment the `await using` scope ends, not merely that the method returned
without throwing.

## `ConfigureAwait(false)` on the way out

Every `await` inside `DisposeAsync` in this chapter's example carries
`.ConfigureAwait(false)`. An `await` without it tries to resume the rest of
the method back on whatever `SynchronizationContext` or `TaskScheduler` was
current when the `await` started, a behavior a UI framework needs so
continuations land back on the UI thread. A library method has no UI
thread to get back to and no caller-specific context it has any business
assuming exists; forcing a resumption onto a context the library did not
set up and does not control is unnecessary scheduling overhead at best, and
a contributor to deadlocks at worst, when that context is single-threaded
and already busy waiting on this same call. The
[Implement a `DisposeAsync` method documentation](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync)
shows this exact idiom in its own sample code, awaiting an inner
`DisposeAsync` call with `ConfigureAwait(false)` before setting a disposed
flag, for the same reason this chapter's example does it: disposal code is
library code, and library code should not assume anything about the
context it was called from. The
[`Task.ConfigureAwait` API reference](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait)
documents the parameter directly: passing `false` means the continuation
"doesn't [need to] run on the original context," which is precisely true of
everything `LedgerAppender.DisposeAsync` does after the `await`, a single
field assignment with no dependency on any particular thread. Applying the
same call consistently, on every `await` in every async method a library
exposes rather than only the ones a developer remembers to check, is what
keeps the habit from becoming a source of subtle bugs that only show up
when some future caller happens to run the library from a context that
does matter.

## Applying it: `ManifestWriter` and `LedgerAppender` side by side

The two types in this chapter's example look different on the surface,
synchronous against asynchronous, `FileStream` opened plainly against a
`FileStream` opened with `FileOptions.Asynchronous` wrapped in a
`StreamWriter`, but they apply the identical reasoning to two different
constructors on `FileStream`. `ManifestWriter` opens its file with
`FileShare.None` and writes and flushes synchronously, because a manifest
entry is a small, infrequent write where blocking the calling thread for
the few milliseconds a local disk write takes costs nothing worth avoiding.
`LedgerAppender` opens its file with the same `FileShare.None` exclusivity
but adds `FileOptions.Asynchronous`, the flag the runtime needs set before
asynchronous reads and writes on that handle actually run asynchronously
rather than silently falling back to a blocking call dressed up in a `Task`
wrapper; a booking ledger in a real system sits on a hotter write path than
an occasional manifest line, and that is where the cost of an async-capable
handle starts paying for itself.

What stays identical between them is everything this chapter covered
before that difference: both are `sealed`, both own exactly one managed
`IDisposable` (a `FileStream` directly, or a `StreamWriter` wrapping one),
both carry a `_disposed` field checked with `ObjectDisposedException.ThrowIf`
at the top of every public member, and neither needs a finalizer or a
`Dispose(bool)` overload, for the same reason given for `ManifestWriter`
earlier: no subclass can add an unmanaged resource underneath a sealed
type, and the one resource each type owns already finalizes itself if
`Dispose` or `DisposeAsync` is never called. The only axis that actually
decides which interface a type implements is whether releasing its
resource requires awaiting something. `ManifestWriter`'s `Stream.Dispose()`
call is synchronous because flushing an already-flushed, synchronously
written stream and closing its handle is itself synchronous work; the
moment a release has to await a flush, as `LedgerAppender`'s does,
`IAsyncDisposable` is the only contract that can express that honestly
without blocking a thread to fake being synchronous.

## Build, run, observe

The example behind this chapter lives in
`examples/07-dispose-and-iasyncdisposable` and is standalone-testable from
its own directory, the same as every other chapter's example in this book:

```sh
cd examples/07-dispose-and-iasyncdisposable
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`. Observed
locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 7
  failed: 0
  succeeded: 7
  skipped: 0
  duration: 905ms
```

The seven cases split across the two types this chapter covers.
`ManifestWriterTests` proves three things behaviorally rather than by
inspection: the manifest file is locked to a second exclusive opener for
the entire `using` scope and free the instant that scope exits, which is
the deterministic-cleanup claim this chapter opened with, not a claim about
garbage collection; writing to a disposed `ManifestWriter` throws
`ObjectDisposedException`, which is the guard clause doing its job; and
disposing twice is harmless, which is the idempotency guard doing its job.
`LedgerAppenderTests` proves the same three shapes for the asynchronous
side, plus one more: after an `await using` scope ends, the file on disk
actually contains the line that was appended inside it, because
`DisposeAsync` awaited the flush before the scope let control continue.
None of these seven tests asserts that a method merely returned without
throwing; each one checks the file system or the thrown exception type
directly, which is what turns "deterministic cleanup" from a claim in this
chapter's prose into something a test run demonstrates on every commit.

## When not to use it

Not every type that touches something releasable needs to become a
first-class `IDisposable` or `IAsyncDisposable`. A `ManifestEntry`, the
`readonly record struct` this chapter's writer appends, holds a shipment
ID, a destination string, and a weight; none of those fields owns anything
that needs releasing, and giving it a `Dispose` method because "it flows
through code that also happens to dispose things" adds a method with
nothing to do and a `using` block at every call site with nothing to
release. The same restraint applies to short-lived helper types built
around an already-disposable collaborator: a method that opens a
`ManifestWriter`, writes a handful of lines, and returns needs a `using`
around that one call, not a second disposable wrapper type built to manage
the first one on the caller's behalf. Wrapping a disposable in another
disposable earns its cost only when the wrapper adds real cleanup of its
own, a second resource, a counter that needs flushing, state that outlives
a single call; otherwise it is an extra type and an extra `using` block
standing between a caller and the resource it already knows how to release
correctly by itself.

The full six-member dispose pattern, finalizer included, is the easier
mistake to make in the other direction. A type gains a finalizer "to be
safe" even though it owns no unmanaged resource and cannot be subclassed
in any way that would add one, and that finalizer now runs on every
instance that reaches the finalization queue, whether or not `Dispose` was
ever skipped, slowing down collection for a safety guarantee the type
never needed in the first place. `ManifestWriter` and `LedgerAppender` both
show the smaller, correct shape: implement the single interface that
matches whether releasing the resource requires an `await`, guard it with
one boolean field, and stop there unless an unmanaged handle or a
subclassing requirement actually shows up to justify more.

> **AI sidebar.** Ask an AI pairing partner to "make this class disposable"
> and a common result is the full six-member pattern, finalizer,
> `Dispose(bool)`, and `GC.SuppressFinalize`, pasted in regardless of
> whether the class is sealed or owns anything unmanaged, because that
> shape is the one most often reproduced in training material under the
> heading "the dispose pattern." The same prompt, aimed at a class with one
> `await`-based cleanup step, will sometimes produce a synchronous
> `Dispose()` that blocks on `.GetAwaiter().GetResult()` internally rather
> than reaching for `IAsyncDisposable`, trading a clean async contract for
> a blocking call with deadlock risk baked in. Check a generated dispose
> implementation against the two questions this chapter actually turns on:
> does release require awaiting something, which decides the interface,
> and does this exact type own an unmanaged resource directly or allow
> subclassing, which is the only thing that justifies the finalizer and
> `Dispose(bool)` overload CA1063 and CA1816 exist to gate. A generated
> finalizer on a sealed class with one managed field, or a blocking
> `Dispose()` wrapped around what should have been `DisposeAsync`, is
> solving a safety problem the class does not have while introducing a
> performance or correctness problem it did not have before.

## For deeper coverage

- [Implement a `Dispose` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose) and [Implement a `DisposeAsync` method](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync) on Microsoft Learn: the canonical shape of the synchronous and asynchronous dispose patterns, including the `ConfigureAwait(false)` idiom this chapter's example follows.
- The [`IDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.idisposable) and [`IAsyncDisposable`](https://learn.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface references, the [`using` statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using) language reference, and [Cleaning up unmanaged resources](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/unmanaged): the primary sources this chapter cites for every claim about scope-exit timing versus garbage collection.
- [CA1063: Implement `IDisposable` correctly](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1063) and [CA1816: Call `GC.SuppressFinalize` correctly](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1816): the analyzer rules behind this chapter's claim that a sealed class owning only managed disposables should not carry a finalizer.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
