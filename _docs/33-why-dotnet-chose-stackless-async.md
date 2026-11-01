---
title: "Why .NET chose stackless async"
order: 33
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "The framing chapter: green threads prototyped and rejected, and why that choice shapes every pattern that follows."
---

<!-- EXAMPLE (shipping domain): Booking throughput against a single sailing. -->

## Learning objectives

By the end of this chapter you can:

- State what a suspended `await` actually costs at runtime: a small
  heap-allocated state machine, not a parked thread, and explain why that
  distinction is the whole reason `async`/`await` scales the way it does.
- Trace `SailingBookingDesk.AcceptAsync` through the compiler's
  transformation from a method written as straight-line code into a state
  machine that suspends at its single `await` and resumes when
  `IBookingLedger.ConfirmAsync` completes.
- Explain why `AcceptAllAsync` can start a thousand bookings concurrently
  from one calling thread without reserving a thousand threads, and point
  to `GatedBookingLedger.DistinctPreAwaitThreadCount` as the test evidence
  for it.
- Describe why `HasCapacityAsync` returns `ValueTask<bool>` and completes
  with no `Task` allocation at all when the answer is already known.
- Name the .NET team's 2022 green-threads prototype, say what it tried to
  do differently, and explain why the team kept the stackless model instead
  of shipping it.

## Why this matters

Every pattern in this book's concurrency material sits on top
of one foundational choice: an `async` method in C# does not run on its own
thread. It compiles into a state machine, a small object on the heap that
remembers where execution paused and what local state it needs to resume.
That choice is easy to take for granted once `async`/`await` feels natural,
but it was a real design decision with a real alternative, and .NET's own
history includes a serious attempt at that alternative. Understanding why
the stackless model won explains why a booking desk can hold a thousand
in-flight confirmations on a handful of thread-pool threads, why a
`CancellationToken` check matters before the first `await` and not only
after it, and why `ValueTask<TResult>` exists as a distinct type instead of
every async method simply returning `Task<TResult>`.

The example behind this chapter is a sailing booking desk,
`SailingBookingDesk`, that accepts consignments onto a single sailing by
confirming each one against an `IBookingLedger`. A real ledger confirmation
is I/O-bound work: a network round trip to a booking system, a database
write, something that takes real wall-clock time during which the thread
that initiated it has nothing useful to do if it just sits there waiting.
`AcceptAsync` awaits that confirmation once per booking, and `AcceptAllAsync`
fans a whole batch of bookings out to run concurrently. What happens to the
calling thread, and to every thread involved, while a thousand of those
confirmations are all in flight at once, is this chapter's subject.

{% include excalidraw.html name="33-why-dotnet-chose-stackless-async" caption="Figure 33.1: A thousand SailingBookingDesk bookings suspend at IBookingLedger.ConfirmAsync and resume later, each one costing a small heap state machine rather than a dedicated thread." %}

## A method that compiles into a state machine

`SailingBookingDesk.AcceptAsync` reads like ordinary sequential code:

```csharp
public async Task<BookingReceipt> AcceptAsync(BookingRequest request, CancellationToken ct = default)
{
    var seq = await ledger.ConfirmAsync(request.ConsignmentId, ct);
    Interlocked.Increment(ref _accepted);
    return new BookingReceipt(request.ConsignmentId, seq);
}
```

Nothing about this method's text mentions a state machine, a thread pool,
or a continuation. That is the entire point of `async`/`await` as a
language feature: the compiler reads the `await` and rewrites the method
behind the scenes into something that can pause at that point and resume
later, while the source stays readable as a single top-to-bottom sequence.
What the compiler produces is a struct or class implementing a state
machine interface, with a numbered state field tracking which `await` (if
any) execution is currently suspended at, fields holding every local
variable and parameter the method needs across that suspension, and a
`MoveNext` method containing the original method's logic split at each
`await` boundary. Calling `AcceptAsync` runs the state machine forward from
its initial state until it either finishes synchronously or reaches an
`await` on work that has not completed yet, at which point `MoveNext`
returns, the calling thread is free to do something else, and the state
machine schedules its own `MoveNext` to run again when `ConfirmAsync`'s
task completes.

The state the method needs across the suspension, `request`, `ct`, and
eventually `seq`, lives in fields on that state-machine object rather than
on a call stack frame, because there is no call stack frame still around to
hold them: the thread that called `AcceptAsync` has already moved on to
other work by the time `ConfirmAsync` finishes. A suspended `await` is
therefore not a thread with its stack frozen in place. It is a small object
sitting on the managed heap, holding exactly the state one method needs to
pick back up where it left off, consuming memory measured in tens or
hundreds of bytes rather than the megabyte-scale stack a dedicated thread
reserves. That difference in what gets held across a suspension, an object
sized to the method's own locals against a thread sized to accommodate
whatever that thread might ever need to call into, is the stackless model's
defining property, and it is what makes a thousand concurrent bookings
possible on a handful of threads, as the sections below show directly.

## The alternative the .NET team tried and set aside

The stackless model was not the only option on the table. In 2022 the .NET
team opened and prototyped a project for stackful coroutines under the
working name of green threads: lightweight, cooperatively scheduled threads
with their own growable stacks, runtime-managed rather than operating-system
managed, intended to let ordinary blocking-looking code scale the way
`async`/`await` does without rewriting it into `await` form. A stackful
green thread can suspend in the middle of a deeply nested call, several
frames down from where the operation that blocks actually happens, because
its whole call stack travels with it across the suspension. That is a real
advantage: it means existing synchronous code, including code buried inside
a library a team does not control, could in principle become concurrency-
friendly without being rewritten with `async` and `await` sprinkled through
every method on the call path.

The team concluded the experiment and wound the prototype down the
following year. A green thread
capable of suspending anywhere on its call stack needs that whole stack
kept around in memory for as long as the thread might be suspended, and a
growable stack is a larger, more complex reservation than the fixed-size
state object a stackless state machine allocates for exactly the locals one
method needs. Scaling to the kind of concurrency server workloads already
achieve with `Task`-based async, tens or hundreds of thousands of
outstanding operations, would have meant carrying that larger per-operation
footprint at the same scale. The team's own public results, posted in the
same `dotnet/runtimelab` repository the experiment lived in, are explicit
that the stackless model, already shipping, already tuned, and already the
shape every `async` method in the base class library had been written
against since C# 5, was the better foundation to keep building on rather
than replace. The choice was not between .NET and some other platform's
answer to concurrency. It was .NET examining its own earlier prototype
against the model already in production and choosing to keep what was
already working.

{% include excalidraw.html name="33-stackless-vs-stackful" caption="Figure 33.2: A stackful green thread carries a full growable call stack across every suspension; a stackless async method carries only the small state machine its own locals need." %}

## A thousand bookings, one calling thread, no thread each

`AcceptAllAsync` is where the stackless model's payoff becomes directly
observable:

```csharp
public async Task<IReadOnlyList<BookingReceipt>> AcceptAllAsync(
    IReadOnlyCollection<BookingRequest> requests, CancellationToken ct = default)
{
    var tasks = requests.Select(r => AcceptAsync(r, ct)).ToList();
    return await Task.WhenAll(tasks);
}
```

`Select(r => AcceptAsync(r, ct)).ToList()` enumerates synchronously, on
whatever thread called `AcceptAllAsync`. For each request, it calls
`AcceptAsync`, which runs forward until its own `await` on
`ledger.ConfirmAsync` suspends it and hands back an incomplete `Task`. That
`Task` goes straight into the list; nothing waits for it to finish before
moving on to the next request. By the time `ToList()` returns, every
booking in the batch has started, and every one of them is suspended at the
same `await` point, all without the enumerating thread ever blocking.
`Task.WhenAll(tasks)` then awaits the whole list together, itself
suspending the `AcceptAllAsync` state machine until every booking's task
has completed.

The test suite proves this directly rather than asserting it from theory.
`GatedBookingLedger` stands in for `IBookingLedger` with every confirmation
parked behind a shared `TaskCompletionSource` gate that the test controls,
plus a thread-id tracker recording which managed thread reached the
pre-await portion of each confirmation. `StacklessModelTests.A_thousand_
bookings_suspend_concurrently_without_a_thread_each` starts a thousand
bookings through `AcceptAsync`, waits for all thousand to arrive at the
gate, and then asserts three things at once: all thousand confirmations
reported arriving, every one of them reached the gate from the same single
thread (`DistinctPreAwaitThreadCount` equals one), and none of the thousand
tasks has completed yet. Only after that assertion does the test release
the gate and await the full batch. A thousand bookings suspended
simultaneously, verified to have started from one thread rather than a
thread pool quietly spinning up a thread per booking to run them, is exactly
the claim a stackful model would have to spend a thousand growable stacks
to make true, and the stackless model makes it true with a thousand small
state-machine objects instead.

{% include excalidraw.html name="33-suspended-without-a-thread" caption="Figure 33.3: A thousand booking requests start from one thread, suspend at the ledger gate with zero threads held, and only resume once the test releases them together." %}

`A_booking_resumes_and_completes_after_the_awaited_confirmation` checks the
other half of the same mechanism at a smaller scale: a single booking
suspends at the gate, stays incomplete (`task.IsCompleted` is `false`)
until the gate releases, and then resumes and produces its receipt once
`ConfirmAsync`'s task completes. Resumption here means the state machine's
`MoveNext` runs again, picking up immediately after the `await`, with
`request` and the newly produced `seq` both still available from the state
machine's own fields exactly as they would be from local variables in
ordinary sequential code. The suspend-and-resume cycle is invisible from
`AcceptAsync`'s source, and that invisibility, not the mechanism underneath,
is what an `async` method is actually optimizing for: write it as if it
runs straight through, and let the compiler carry the state across whatever
suspensions actually happen.

## Skipping the state machine entirely with ValueTask

`HasCapacityAsync` takes a different path from either `AcceptAsync` or
`AcceptAllAsync`:

```csharp
public ValueTask<bool> HasCapacityAsync(CancellationToken ct = default) =>
    new(Volatile.Read(ref _accepted) < sailingCapacity);
```

There is no `await` anywhere in this method and no `async` keyword on it.
Checking remaining capacity reads an already-known counter and compares it
to a known limit, work that finishes before the method returns control to
its caller at all. Returning `Task<bool>` from a method like this would
still work at every call site, since every caller of a `Task<bool>` can
`await` it the same way as any other task, but it would allocate a
`Task<bool>` object on the heap for a result that was never actually
asynchronous. `ValueTask<TResult>` is a struct that can hold a completed
result directly, with no heap allocation at all, and only falls back to
wrapping a real `Task<TResult>` on the rarer path where the work does need
to go asynchronous.

`A_capacity_check_completes_synchronously_without_allocating_a_task` checks
both halves of that claim: it calls `HasCapacityAsync` without awaiting the
result yet, asserts `IsCompletedSuccessfully` is already `true` on the
returned `ValueTask<bool>`, and only then awaits it to confirm the value.
Checking `IsCompletedSuccessfully` before awaiting is what distinguishes
this from merely checking that the eventual value is correct: it proves the
result was available the instant the method returned, with no suspension
and no state machine ever constructed for this call at all. Stackless async
is not only about what a suspended `await` costs. It is also about a method
having the option to cost nothing extra at all when the answer is already
in hand, a choice a model built around always-present growable stacks has
less room to offer as cleanly.

## Build, run, observe

The example behind this chapter, `SailingBookingDesk`, `IBookingLedger`,
`InMemoryBookingLedger`, `BookingRequest`, and `BookingReceipt`, is
standalone-testable from its own directory:

```sh
cd examples/33-why-dotnet-chose-stackless-async
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 6
  failed: 0
  succeeded: 6
  skipped: 0
```

The six cases split across two fixtures. `BookingDeskTests` proves a single
booking returns a ledger-sequenced receipt, a thousand concurrent bookings
each receive a distinct sequence number, `HasCapacityAsync` completes
synchronously with no task allocation, and a cancelled token throws before
any confirmation starts. `StacklessModelTests` proves the chapter's central
claim directly: a thousand bookings all suspend at once from a single
calling thread with none of them completing until released together, and a
single booking resumes and produces its receipt once its awaited
confirmation completes.

## When not to use it

The stackless model answers a specific question well: how does one thread
wait on many independent, slow, I/O-bound operations without reserving a
thread per operation. It does not make CPU-bound work faster. A method that
spends its time computing rather than waiting, a routing calculation over a
large sailing schedule or a hash over a large manifest, gains nothing from
being wrapped in `async` and `await`, because there is no idle waiting
period for the thread to be released during. The thread runs the whole
computation regardless of whether the method is declared `async`; wrapping
it just adds a state machine around work that was never going to suspend.
Parallelism across multiple threads, not asynchrony on one thread, is the
tool for CPU-bound work, and it is a different problem from the one
`AcceptAsync` solves by awaiting `ConfirmAsync`.

A related trap is wrapping purely synchronous or blocking work in
`Task.Run` purely to produce an `async`-shaped signature, sometimes called
async-over-sync. `Task.Run` does move work off the calling thread, but onto
a thread-pool thread that then blocks for the whole duration of whatever
synchronous call it is running, which is strictly worse than running that
same call on the original thread: it burns a pool thread for the duration
and adds a scheduling hop that buys nothing, because the thread doing the
blocking still cannot do other work while it waits. `ConfirmAsync` only
earns its `async` signature because `Task.Yield` truly suspends it and
frees the thread; a method with no real suspension point gains nothing from
the same shape.

Fire-and-forget calls, an `async` method invoked without ever awaiting or
otherwise observing its returned task, drop the one guarantee `Task` exists
to provide: knowing when the work finished and whether it threw. A booking
desk that called `ledger.ConfirmAsync` and discarded the result without
awaiting it would have no way to know a confirmation failed, and an
exception thrown inside that abandoned task would either vanish silently or
surface later as an unobserved task exception with no connection back to
the booking that caused it. `AcceptAsync` always awaits its single
confirmation and always propagates whatever that await produces, success or
exception, back to its own caller, which is what keeps a cancelled token or
a ledger failure visible instead of lost.

<blockquote class="ai-sidebar" markdown="1">
An AI pairing partner is useful for two narrow jobs here: converting an
existing synchronous method to `async`/`await` mechanically, threading an
`await` and a `Task`-shaped return type through a call chain without
missing a caller, and scanning a method body for blocking calls, a
synchronous file read, a `.Result` or `.Wait()` on a task, a `Thread.Sleep`,
that would stall the state machine's resuming thread instead of yielding
it. Neither job answers the question that actually decides whether a
method should be `async` at all: whether the work it does is truly
I/O-bound and therefore has an idle period worth releasing the thread
during, or CPU-bound and therefore gains nothing from the conversion no
matter how mechanically correct the resulting `async`/`await` syntax looks.
`HasCapacityAsync` is also a case worth checking by hand: it stays
synchronous in its usual path by design, and an assistant asked to "make
it async" might wrap it in `Task.Run` or `Task.FromResult` without noticing
`ValueTask<bool>` already serves the method better with no allocation at
all. That judgment call belongs to the developer, not the suggestion.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Asynchronous programming with async and await](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/): the authority for the `async`/`await` language feature and the state-machine transformation behind `AcceptAsync`.
- Microsoft Learn, [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap): the task-based async model `AcceptAsync` and `AcceptAllAsync` are written against.
- Microsoft Learn, [ValueTask\<TResult\> Struct](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1): the authority for when a `ValueTask<TResult>` return avoids the allocation `HasCapacityAsync` sidesteps.
- `dotnet/runtimelab`, [Green Thread Experiment](https://github.com/dotnet/runtimelab/issues/2057): the .NET team's 2022 tracking issue for the stackful coroutine prototype this chapter calls green threads.
- `dotnet/runtimelab`, [Green Thread Experiment Results](https://github.com/dotnet/runtimelab/issues/2398): the team's own writeup of why the prototype was wound down in favor of the stackless model this chapter's example is built on.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
