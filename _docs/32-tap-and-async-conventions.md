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

- Name the conventions of the Task-based Asynchronous Pattern and find each one on `ICarrierGateway`: the `Async` suffix, a `Task` or `ValueTask` return type, and a trailing `CancellationToken`.
- Write an async method that honors a `CancellationToken` instead of only accepting it.
- Report progress from a long-running operation through `IProgress<T>`, and know why `Progress<T>` is the wrong double for asserting on reports.
- Choose `ValueTask<bool>` over `Task<bool>` when the answer is usually known before the method returns.
- Apply `ConfigureAwait(false)` in library code and say what it avoids.

## Why this matters

Nothing in C# forces an async method to take a particular name, return type or parameter list. The Task-based Asynchronous Pattern (TAP) is the platform's documented answer: one shape for every asynchronous member, specified on Microsoft Learn and followed throughout the base class library. Learn it once and every TAP-conforming API reads the same way.

The shape:

- a name ending in `Async`;
- a return type of `Task`, `Task<TResult>`, `ValueTask` or `ValueTask<TResult>`;
- a trailing `CancellationToken` when cancellation makes sense, which the method checks;
- an optional `IProgress<T>` for an operation long enough to report partial progress.

None of these changes what the method computes. They make the surface predictable: a caller who sees `DispatchAsync` returning `Task<DispatchReceipt>` knows to await it, knows a cancelled token stops it early, and knows an `IProgress<T>` argument will receive updates, all without reading the body.

{% include excalidraw.html name="32-tap-and-async-conventions" caption="Figure 32.1: ICarrierGateway follows the Task-based Asynchronous Pattern: every async member ends in Async, returns a Task or ValueTask, and accepts a cancellation token, so a fake implementation can stand in cleanly." %}

## Three conventions on one interface

`ICarrierGateway` is the whole async surface a dispatch workflow talks to:

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

Every member ends in `Async` and returns a task-like type, never a bare result with the asynchrony hidden behind a callback. Every member takes a `CancellationToken` with a `default`, so most call sites can omit it. `DispatchBatchAsync` adds an optional `IProgress<DispatchProgress>` because it's the one operation long enough to report on.

Naming conventions drift once an interface grows. A reflection check over `typeof(ICarrierGateway)` enforces both the suffix and the token parameter: a new member that returns `Task<Something>` without the suffix or the token fails before any implementation exists.

## Honoring cancellation, not just accepting it

Accepting a token costs nothing: a method that never reads it compiles and runs the same. Honoring it is what keeps the promise. `FakeCarrierGateway.DispatchAsync` checks once up front and passes the token into the one await it performs:

```csharp
public async Task<DispatchReceipt> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    await Task.Delay(0, cancellationToken).ConfigureAwait(false);

    return new DispatchReceipt(request.ConsignmentId, ToTrackingNumber(request.ConsignmentId), acceptedAt);
}
```

An already-cancelled token throws `OperationCanceledException` before the body runs. Passing the token to `Task.Delay` makes the fake behave like the I/O it stands in for; a fake that ignored the token would let cancellation tests pass for the wrong reason.

`DispatchBatchAsync` checks once per item, because a token valid at the start can be cancelled partway through:

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

A caller who cancels mid-batch wants the remaining items left undispatched, not finished and followed by an exception. With the check inside the loop, a progress callback that cancels on its first report stops a three-item batch after one item.

{% include excalidraw.html name="32-cancellation-and-progress" caption="Figure 32.2: Dispatching a batch checks for cancellation before each item and reports progress after it, so a caller watching the progress consumer sees steady updates until a cancellation stops the loop." %}

## Progress reporting without surprises

`DispatchBatchAsync` reports once per item, after that item's receipt is in hand, with a `DispatchProgress` record of items done and items total. Reports arrive in order, each `Completed` one more than the last, and the final report has `Completed` equal to `Total`. The parameter defaults to `null`, and a batch with no progress consumer still dispatches every request.

The two `IProgress<T>` implementations behave differently:

| Implementation | Where `Report` runs | Use it for |
| --- | --- | --- |
| `Progress<T>` | Posted to the `SynchronizationContext` captured at construction, usually the UI thread | Application code driving a progress bar |
| A recording double that appends to a list | Synchronously, on the reporting thread | Tests that inspect reports right after the await |

`Progress<T>` runs its callback asynchronously relative to the caller, so code that awaits the batch and then reads the reports can race reports still in flight. A synchronous double has every report recorded by the time the await completes.

## ValueTask where completion is usually synchronous

A healthy gateway answers reachability from cached state, before the method returns. A `Task<bool>` allocates a heap object for that result even though it was ready at once. `ValueTask<TResult>` is a struct that holds a completed result directly and wraps a real task only when the work goes asynchronous:

```csharp
public ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    return new ValueTask<bool>(true);
}
```

The returned value reports `IsCompleted` as `true` before anyone awaits it. Callers await it exactly as they would a `Task<bool>`.

| Member | Return type | Why |
| --- | --- | --- |
| `DispatchAsync` | `Task<DispatchReceipt>` | Always awaits; no synchronous path to optimize |
| `DispatchBatchAsync` | `Task<IReadOnlyList<DispatchReceipt>>` | Always awaits |
| `IsReachableAsync` | `ValueTask<bool>` | Async in principle, synchronous in most calls |

{% include excalidraw.html name="32-task-valuetask-shape" caption="Figure 32.3: DispatchAsync returns a Task because it awaits real work, while IsReachableAsync returns an already-completed ValueTask on its usual synchronous path, avoiding an allocation." %}

## Library code and ConfigureAwait(false)

Every await inside `FakeCarrierGateway` ends in `.ConfigureAwait(false)`. By default, awaiting a task captures the current `SynchronizationContext`, when there is one, and resumes the continuation on it. A button-click handler wants that, so code after the await can touch UI elements. A gateway has no UI and no opinion about which thread continues it. `ConfigureAwait(false)` skips the capture, and the continuation resumes on any free thread-pool thread.

The convention belongs to library code. Application code that needs its continuation back on a UI or request context leaves it out, because adding it removes the behavior the context exists for. A real carrier integration built on `ICarrierGateway` runs inside whatever application hosts it, so it shouldn't assume that application's threading model.

None of the three members runs dispatches concurrently or guards shared state. Composing TAP calls into concurrent pipelines and protecting state that several async calls touch are separate problems from naming, cancellation, progress and return type.

## Build, run, observe

```sh
cd examples/32-tap-and-async-conventions
dotnet test
```

All 10 tests pass; `scripts/verify.sh` at the repo root runs them with every other example. To see the convention check work, add a member to `ICarrierGateway` that returns `Task<bool>` without the `Async` suffix: the reflection check fails.

## When not to use it

- **No async path at all.** A pure computation over values in hand gains nothing from `async` and `Task<TResult>`; the wrapper adds an allocation and a state machine. Keep it synchronous rather than returning `ValueTask<TResult>` to match its neighbors.
- **A token nothing checks.** A `CancellationToken` that's never checked or forwarded tells callers the method cancels when it doesn't. It compiles and passes every case that never cancels.
- **A progress parameter nothing calls.** An `IProgress<T>` that never receives `Report` leaves a progress bar at zero until the batch ends. Remove plumbing that implies a capability the method lacks.

<blockquote class="ai-sidebar" markdown="1">
**Specifying an async member.** "Make the gateway async" doesn't say where cancellation is checked, when progress is reported or which members await real work. State them:

> In `FakeCarrierGateway`, implement `DispatchBatchAsync(IReadOnlyList<DispatchRequest>, IProgress<DispatchProgress>?, CancellationToken)`. Call `cancellationToken.ThrowIfCancellationRequested()` before each request, pass the token to every await and end each await with `ConfigureAwait(false)`. After each receipt, call `progress?.Report(new DispatchProgress(i + 1, requests.Count))`. Implement `IsReachableAsync` without `async`, returning a completed `ValueTask<bool>`. Don't wrap anything in `Task.Run`, and keep the `Async` suffix and a trailing `CancellationToken` on every member of `ICarrierGateway`.

Run `dotnet test --project CarrierDispatch.Tests --filter "BatchProgressTests|AsyncConventionTests"`; all 8 must pass.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap): the authority for the pattern, including the naming, parameter, and return-type conventions `ICarrierGateway` follows.
- Microsoft Learn, [Task.ConfigureAwait(Boolean) Method](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait): the authority for what `ConfigureAwait(false)` changes about continuation scheduling and why library code reaches for it.
- Microsoft Learn, [ValueTask\<TResult\> Struct](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1): the authority for when a `ValueTask<TResult>` return type is appropriate in place of `Task<TResult>`, the choice behind `IsReachableAsync`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
