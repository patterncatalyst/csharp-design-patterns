---
title: "TAP and the async conventions"
order: 32
layout: tutorial
sectionid: docs
part: "platform-patterns"
description: "The Task-based Asynchronous Pattern named: naming, cancellation, and progress conventions, with mechanics left to the concurrency part."
---

<!-- EXAMPLE (shipping domain): The carrier gateway's async API surface. -->

## Learning objectives

By the end of this chapter you can:

- Name the three conventions that make up the Task-based Asynchronous
  Pattern and point to each one on `ICarrierGateway`: the `Async` suffix, a
  `Task`, `Task<TResult>`, or `ValueTask<TResult>` return type, and a
  trailing `CancellationToken` parameter.
- Write an async method that honours a `CancellationToken` rather than
  merely accepting one, the way `FakeCarrierGateway.DispatchBatchAsync`
  checks `ThrowIfCancellationRequested` before each item in its loop.
- Report incremental progress from a long-running async operation through
  `IProgress<T>`, and explain why a test that asserts on progress reports
  needs its own synchronous `IProgress<T>` double rather than the built-in
  `Progress<T>`.
- Explain why `IsReachableAsync` returns `ValueTask<bool>` instead of
  `Task<bool>`, and what that return type buys a caller when the answer is
  usually already known by the time the call returns.
- Apply `ConfigureAwait(false)` inside library code such as
  `FakeCarrierGateway`, and state the problem it solves for a caller that
  has its own ideas about which thread should run next.

## Why this matters

An async method in C# can look however its author likes: any name, any
return type, any parameter list. Nothing in the language forces an async
method to end in a particular suffix or accept a particular kind of
cancellation signal. The Task-based Asynchronous Pattern, TAP, is the
platform's own answer to that freedom: a named, documented set of
conventions for what an asynchronous member should look like, so that two
unrelated libraries can each expose async work in a shape every caller
already recognizes. TAP is not a convention one team invented for its own
codebase. It is specified by Microsoft Learn and followed throughout the
base class library itself, so a developer who learns the shape once reads
every TAP-conforming API the same way afterward.

That shape is short: a method name ending in `Async`, a return type of
`Task`, `Task<TResult>`, `ValueTask`, or `ValueTask<TResult>`, and a
trailing `CancellationToken` parameter when cancellation makes sense for
the operation. Two further conventions round it out for the operations
that need them: a `CancellationToken` that the method checks and responds
to, not just accepts, and an optional `IProgress<T>` parameter for an
operation long enough that a caller benefits from incremental updates
before the final result arrives. None of these conventions change what an
async method computes. What they change is how predictable the surface
around that computation is: a caller who sees `DispatchAsync` returning
`Task<DispatchReceipt>` already knows to `await` it, already knows a
cancelled `CancellationToken` will stop it early, and already knows an
`IProgress<T>` parameter, if present, will report partial results along
the way, none of it requiring a look at the method body first.

This chapter builds a small carrier-booking gateway around that shape.
`ICarrierGateway` exposes three members: a single dispatch that goes
through the full `Task<TResult>` ceremony, a batch dispatch that adds
cancellation and progress reporting on top, and a reachability check that
usually has its answer before it ever needs to await anything. The rest of
the chapter reads what each member does with naming, cancellation,
progress, and the return-type choice between `Task<TResult>` and
`ValueTask<TResult>`, closing with the one convention aimed at the library
author rather than the caller, `ConfigureAwait(false)`.

{% include excalidraw.html name="32-tap-and-async-conventions" caption="Figure 32.1: ICarrierGateway follows the Task-based Asynchronous Pattern: every async member ends in Async, returns a Task or ValueTask, and accepts a cancellation token, so a fake implementation can stand in cleanly." %}

## Three conventions on one interface

The example behind this chapter lives in
`examples/32-tap-and-async-conventions`. `ICarrierGateway` is the entire
async surface a dispatch workflow talks to:

```csharp
public interface ICarrierGateway
{
    Task<DispatchReceipt> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispatchReceipt>> DispatchBatchAsync(
        IReadOnlyList<DispatchRequest> requests,
        IProgress<DispatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default);
}
```

Every member ends in `Async`. Every member returns a `Task<TResult>` or a
`ValueTask<TResult>`, never a bare `TResult` with the asynchrony hidden
behind an out parameter or a callback. Every member accepts a
`CancellationToken` with a `default`, so most call sites can call any of
the three without supplying one explicitly. `DispatchBatchAsync` adds the
fourth convention, an optional `IProgress<DispatchProgress>` for the one
operation long enough to make incremental updates worth reporting.

Naming and parameter conventions are easy to state and just as easy to
drift away from once an interface grows past the first few members someone
reviewed carefully. `AsyncConventionTests` in the example checks both
conventions with reflection rather than by inspection, so a new member that
breaks either one fails the suite instead of waiting for a reviewer to
notice:

```csharp
private static bool IsTaskLike(MethodInfo method)
{
    var returnType = method.ReturnType;
    if (returnType == typeof(Task) || returnType == typeof(ValueTask))
    {
        return true;
    }

    return returnType.IsGenericType
        && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
            || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
}
```

`Every_task_returning_member_uses_the_async_suffix` calls `GetMethods()` on
`typeof(ICarrierGateway)`, filters to the ones `IsTaskLike` recognizes, and
asserts every name ends with `Async`. `Every_async_member_accepts_a_
cancellation_token` runs the same filtered set through a second assertion,
that each method's parameter list contains a `CancellationToken`. Together
the two tests turn conventions that are easy to state in prose into
something the suite enforces on the interface's own shape. Add a fourth
member to `ICarrierGateway` that returns `Task<Something>` but forgets the
`Async` suffix or the `CancellationToken` parameter, and one of these two
tests fails before any implementation is written.

## Honouring cancellation, not just accepting it

Accepting a `CancellationToken` parameter is the half of the cancellation
convention that costs nothing: the parameter sits there with a `default`
value, and a method that never reads it still compiles and runs exactly as
it would without one. Honouring it is the other half, the half that keeps
the convention's promise to a caller. `FakeCarrierGateway.DispatchAsync`
honours its token twice, once up front and once inside the single await it
performs:

```csharp
public async Task<DispatchReceipt> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    await Task.Delay(0, cancellationToken).ConfigureAwait(false);

    return new DispatchReceipt(request.ConsignmentId, ToTrackingNumber(request.ConsignmentId), acceptedAt);
}
```

`ThrowIfCancellationRequested` at the top means a caller who hands in an
already-cancelled token never reaches the body at all. Passing the same
token into `Task.Delay` means the delay observes cancellation even on a
gateway with no real network call to cancel; a fake standing in for I/O
that would normally respond to the token still needs to behave as if it
does, or a test exercising cancellation against the fake would pass for
the wrong reason. `DispatchAsyncTests.DispatchAsync_honours_a_cancelled_
token` proves the first check works: it cancels a `CancellationTokenSource`
before calling `DispatchAsync` at all and asserts the call throws an
`OperationCanceledException` rather than returning a receipt for a request
nobody asked to complete.

`DispatchBatchAsync` repeats the same check once per item, not only once
at the start, because a batch is the one place where checking early is not
enough:

```csharp
var receipts = new List<DispatchReceipt>(requests.Count);
for (var i = 0; i < requests.Count; i++)
{
    cancellationToken.ThrowIfCancellationRequested();

    var request = requests[i];
    await Task.Delay(0, cancellationToken).ConfigureAwait(false);
    receipts.Add(new DispatchReceipt(request.ConsignmentId, ToTrackingNumber(request.ConsignmentId), acceptedAt));

    progress?.Report(new DispatchProgress(i + 1, requests.Count));
}
```

A token valid when `DispatchBatchAsync` started can still be cancelled
partway through a ten-item batch, and a caller who cancelled it is asking
the loop to stop dispatching the remaining items, not to finish the ones
already in flight and then throw at the end. Checking before each
iteration rather than only before the loop is what makes that difference
real. `BatchProgressTests.DispatchBatchAsync_honours_cancellation_mid_
batch` proves it with a progress callback that cancels its own source the
moment it records its first report, `CancelOnFirstReportProgress`. Because
`DispatchBatchAsync` checks the token again at the top of its next
iteration, the batch stops after the first item rather than running the
remaining two, and the test asserts the recorded report count stays below
the total request count, evidence that the per-iteration check stopped
real work rather than only changing what exception a caller sees at the
end.

{% include excalidraw.html name="32-cancellation-and-progress" caption="Figure 32.2: Dispatching a batch checks for cancellation before each item and reports progress after it, so a caller watching the progress consumer sees steady updates until a cancellation stops the loop." %}

## Progress reporting without surprises

`IProgress<DispatchProgress>` is the convention for an async operation
that runs long enough for a caller to want to know how far along it is
before the final `Task<TResult>` completes. `DispatchBatchAsync` reports
once per item, after that item's receipt is already in hand, with a
`DispatchProgress` record carrying how many items are done and how many
total. The parameter is nullable with a `default` of `null`, so a caller
with nothing to do with progress pays nothing for the convention beyond
the parameter existing. `BatchProgressTests.DispatchBatchAsync_without_a_
progress_callback_still_completes` confirms leaving `progress` as `null`
is a supported call shape: the batch still dispatches every request and
returns a full list of receipts with no progress consumer attached.

The built-in `Progress<T>` type is the usual choice for consuming these
reports in an application, because its constructor captures the calling
thread's `SynchronizationContext` and marshals every `Report` call back
onto that context, typically the UI thread, so a progress bar can update
without the reporting code knowing anything about threads. That same
marshaling makes `Progress<T>` the wrong double for a test asserting on
report contents: the callback runs asynchronously relative to the method
posting it, so a test that awaits `DispatchBatchAsync` and immediately
inspects a list of reports can race against reports still in flight.
`RecordingProgress`, in the test project, appends directly to a list on
the calling thread, with no context capture and no marshaling, so by the
time `DispatchBatchAsync`'s `await` completes, every report it sent is
already recorded.

`BatchProgressTests.DispatchBatchAsync_reports_progress_for_each_item` uses
`RecordingProgress` to assert the report count matches the request count
and the final report's `Completed` equals its `Total`. `Progress_reports_
are_monotonic` checks the stronger property that matters for a progress
bar or a log line built from these reports: each report's `Completed`
value is exactly one more than the previous report's, in order, with none
skipped and none repeated, because `DispatchBatchAsync`'s loop reports
once per completed item, strictly in the order the items were dispatched.

## ValueTask where completion is usually synchronous

`IsReachableAsync` returns `ValueTask<bool>` instead of `Task<bool>`
because its usual answer does not require awaiting anything. A healthy
gateway can report reachability from cached state or a cheap local check,
work that completes before the method returns control to its caller, and
a `Task<TResult>` allocates a heap object to represent that result even
when it was available immediately. `ValueTask<TResult>` is a struct that
can hold a completed result directly, with no allocation, and only falls
back to wrapping an actual `Task<TResult>` when the work needs to go
asynchronous. `FakeCarrierGateway.IsReachableAsync` takes the fully
synchronous path every time:

```csharp
public ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    return new ValueTask<bool>(true);
}
```

`AsyncConventionTests.IsReachableAsync_completes_synchronously` checks both
halves of that claim in one test: it calls `IsReachableAsync` without
awaiting the result yet, asserts `IsCompleted` is already `true` on the
returned `ValueTask<bool>`, and only then awaits it to confirm the value is
`true`. A method returning `Task<bool>` would behave identically at the
call site, every caller of `ValueTask<bool>` can `await` it the same way,
but it would allocate a `Task<bool>` on every call regardless of how the
answer was produced. `ValueTask<TResult>` is the TAP convention for an
operation that is async in principle, because a caller cannot assume it
will never need to go async in the future, but synchronous in the
overwhelming majority of real calls. `DispatchAsync` and
`DispatchBatchAsync` stay on `Task<TResult>` because they always await
something, so there is no synchronous path for `ValueTask<TResult>` to pay
off against.

{% include excalidraw.html name="32-task-valuetask-shape" caption="Figure 32.3: DispatchAsync returns a Task because it awaits real work, while IsReachableAsync returns an already-completed ValueTask on its usual synchronous path, avoiding an allocation." %}

## Library code and ConfigureAwait(false)

Every await inside `FakeCarrierGateway` ends in `.ConfigureAwait(false)`.
Awaiting a `Task` by default captures the current `SynchronizationContext`,
when one exists, and resumes the method's continuation on that same
context once the awaited work finishes, exactly the behavior a UI
application wants: code that runs after an `await` in a button-click
handler should resume on the UI thread so it can touch UI elements safely.
A gateway implementation has no UI to resume onto and no opinion about
which thread runs its continuation. `ConfigureAwait(false)` tells the
awaited `Task` not to capture a context at all, so the continuation
resumes on whatever thread-pool thread is free, skipping a context hop
library code never needed.

The convention is specific to library code precisely because applications
are the other half of it: code that needs its continuation back on a UI or
request context should not add `ConfigureAwait(false)`, since that would
remove the behavior the context exists to provide. `FakeCarrierGateway`
sits on the library side of that line: the convention is written into the
gateway because a real carrier integration built from this interface would
run inside whatever application chose to host it, and should not assume
that application's threading model.

None of `ICarrierGateway`'s three members run two dispatches at once,
coordinate access to shared mutable state, or schedule work onto a
particular thread. Combining several TAP-shaped calls into a pipeline that
runs concurrently, or protecting state multiple async calls touch at the
same time, is a different problem from the naming, cancellation, progress,
and return-type conventions this chapter covers, and it is deferred to the
concurrency material elsewhere in this book.

## Build, run, observe

The example behind this chapter, `ICarrierGateway`, `FakeCarrierGateway`,
`DispatchRequest`, `DispatchReceipt`, and `DispatchProgress`, is
standalone-testable from its own directory:

```sh
cd examples/32-tap-and-async-conventions
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 10
  failed: 0
  succeeded: 10
  skipped: 0
  duration: 728ms
```

The ten cases split across three fixtures. `AsyncConventionTests` proves
the naming and cancellation-parameter conventions hold across the whole
interface by reflection, and that `IsReachableAsync` completes
synchronously. `DispatchAsyncTests` proves a single dispatch returns a
correct receipt and honours a token cancelled before the call even starts.
`BatchProgressTests` proves a batch reports progress for every item in
strict order, completes with no progress consumer attached at all, and
stops partway through when cancellation arrives mid-batch.

## When not to use it

TAP's ceremony, the `Async` suffix, a `Task` or `ValueTask` return, a
`CancellationToken` parameter, and sometimes an `IProgress<T>` parameter,
pays off when a method goes asynchronous along the way, or might need to
in the future, and when a caller can do something useful with cancellation
or progress once offered. An operation that never awaits anything and
never will, a pure computation over values already in hand, gets nothing
from being wrapped in `async` and returning `Task<TResult>` that a plain
synchronous method would not already provide, and the wrapping adds an
allocation and a state machine for no benefit. `IsReachableAsync` sidesteps
that cost with `ValueTask<bool>` because its typical answer is
synchronous; a method with no plausible async path at all does better
staying synchronous rather than reaching for `ValueTask<TResult>` just to
look consistent with its neighbours.

A `CancellationToken` or `IProgress<T>` parameter that nothing checks or
calls is worse than not having the parameter at all, because it tells a
caller the method supports cancellation or progress reporting when it does
not. `FakeCarrierGateway.DispatchAsync` checks its token twice and
forwards it into `Task.Delay`; a method that accepted the same parameter
and ignored it would compile identically and pass every test that never
cancels, while quietly failing the one that does. The same risk applies to
`IProgress<T>`: a batch operation that accepts a progress parameter but
never calls `Report` on it leaves a progress bar stuck at zero until the
whole batch finishes. Plumbing that nothing reads on one end and nothing
writes on the other should come out of the signature rather than stay as
an unused parameter implying a capability the method does not have.

<blockquote class="ai-sidebar" markdown="1">
Asked to make a slow method "async," an AI pairing
partner will sometimes wrap a purely synchronous computation in
`Task.Run` and return the resulting `Task<TResult>`, turning a
computation with no I/O at all into one that burns a thread-pool thread
to produce a result that was already available without awaiting
anything. A related suggestion adds a `CancellationToken` parameter to
satisfy the TAP shape and then never calls `ThrowIfCancellationRequested`
or passes the token into anything awaited, which compiles cleanly and
passes every test that never cancels while silently failing the one that
does, exactly the gap `DispatchAsyncTests.DispatchAsync_honours_a_
cancelled_token` exists to catch. Check a generated async method against
the same two questions this chapter asked of `ICarrierGateway`: does the
work need to run asynchronously at all, or would `ValueTask<TResult>`,
or no `Task` at all, serve it better, and does every
`CancellationToken` or `IProgress<T>` parameter get checked or called
somewhere in the body, or is it a parameter that only looks like support
for a capability the method does not offer.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap): the authority for the pattern this chapter names, including the naming, parameter, and return-type conventions `ICarrierGateway` follows.
- Microsoft Learn, [Task.ConfigureAwait(Boolean) Method](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait): the authority for what `ConfigureAwait(false)` changes about continuation scheduling and why library code reaches for it.
- Microsoft Learn, [ValueTask\<TResult\> Struct](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1): the authority for when a `ValueTask<TResult>` return type is appropriate in place of `Task<TResult>`, the choice behind `IsReachableAsync`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
