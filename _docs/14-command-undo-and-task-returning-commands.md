---
title: "Command, undo, and Task-returning commands"
order: 14
layout: tutorial
sectionid: docs
part: "selecting-varying-behavior"
description: "Delegates as commands, reversible operations, and command queues that return Task."
---

<!-- EXAMPLE (shipping domain): Booking amendments with undo. -->

## Learning objectives

By the end of this chapter you can:

- Explain the GoF Command pattern as a reified operation, an object standing
  in for a method call, and state why that reification is what makes undo
  and queued execution possible that a bare method call never supports.
- Implement a reversible `IBookingCommand` against a mutable `Booking`
  receiver, with `Execute` snapshotting whatever prior state `Undo` needs and
  `Undo` restoring exactly that state.
- Write `DelegateCommand`, the delegate-as-command form, and show it executes
  and undoes equivalently to a hand-written `IBookingCommand` class for the
  same change.
- Build an `AmendmentQueue` that runs queued commands first-in-first-out and
  unwinds its execution history last-in-first-out, and explain why those two
  orders are not the same order read backward by accident.
- State the snapshot rule that makes undo correct under later mutation: a
  command must capture the prior value, not a live reference to a mutable
  field that a later command can still change out from under it.
- Write a Task-returning command, `IAsyncBookingCommand`, that awaits an
  injected gateway with `ConfigureAwait(false)` and a threaded
  `CancellationToken`, and explain what changes and what stays identical
  between a synchronous queue and its asynchronous counterpart.

## Why this matters

A booking amendment is rarely a single, final decision. A customer asks to
bump a shipment from standard to express service, then changes their mind
twenty minutes later and asks to go back to standard. An operations clerk
batches three changes against the same booking, a service-level change,
a weight correction, and a new line item for a rebooking fee, and wants
all three applied together or none of them applied at all if the third one
turns out to be wrong. A finance reconciliation job replays a batch of
amendments against a nightly snapshot, and if one of them was recorded in
error, somebody needs to take it back out without re-deriving every booking
in the batch from scratch.

None of those three scenarios is served by a method call that mutates a
`Booking` directly and forgets what it did. `booking.ServiceLevel =
"Express"` is a statement, not a thing a program can hold onto, inspect,
queue behind other statements, or ask to reverse later. Once that
assignment runs, the only way back to "Standard" is for some other piece of
code to already know what the value used to be, which means every caller
that might ever need to undo a change has to shadow the booking's own state
in a field of its own, rebuilt by hand for every property anyone might
amend. A clerk's batch of three changes fares no better: applying them is
three separate statements with nothing tying them together, and if the
third one needs to be pulled back out, the two before it have no record
that they ran at all, let alone in what order.

The Gang of Four's Command pattern exists for exactly this problem. Its
core move is to take a request, a service-level change, a weight
correction, a line-item addition, and turn it into an object rather than a
direct call: something with an identity, a place to keep whatever
state its own reversal needs, and a uniform shape that lets unrelated kinds
of amendments sit in the same queue, the same history stack, and the same
test suite without the caller needing to know which concrete change a given
command represents. A `ChangeServiceLevelCommand` and an
`AddLineItemCommand` have nothing in common at the level of what they do to
a `Booking`, but they have everything in common at the level of how a queue
or an undo stack treats them: enqueue it, run it, and if asked, undo it.
That uniformity is what makes batching, replay, and undo possible in the
first place, and it is also what lets this chapter extend the same shape to
an amendment that confirms over a network call instead of mutating a field
in memory, without the queue coordinating either kind needing to change at
all.

The example behind this chapter lives in
`examples/14-command-undo-and-task-returning-commands`. A `Booking` is the
plain mutable receiver every command in the chapter targets: a
`BookingId`, a settable `ServiceLevel`, a settable `WeightKilograms`, and a
read-only view over an internally mutable list of `LineItem` values added
through `AddLineItem` and removed through `RemoveLineItem`. Every command
this chapter writes changes exactly one of those three things and knows how
to change it back.

{% include excalidraw.html name="14-command-undo-and-task-returning-commands" caption="Figure 14.1: A booking amendment reified as an IBookingCommand object rather than a direct mutation. ChangeServiceLevelCommand and ChangeWeightCommand snapshot the prior value in Execute and restore it in Undo; AddLineItemCommand's own added value is its snapshot. AmendmentQueue runs enqueued commands FIFO into a history stack and unwinds that stack LIFO on Undo. IAsyncBookingCommand and AsyncAmendmentQueue mirror the same shape over an awaited IAmendmentGateway call instead of an in-memory field assignment." %}

## Command as a reified operation

`IBookingCommand` is the whole contract this chapter builds on, two
methods and nothing else:

```csharp
public interface IBookingCommand
{
    void Execute();

    void Undo();
}
```

There is no `Name` property, no `Description`, no metadata beyond what a
concrete implementation chooses to add on top. The interface's narrowness
is the point: anything that can apply itself and reverse itself qualifies
as a command, regardless of what it changes underneath. `ChangeServiceLevelCommand`
is the simplest case that still needs a snapshot:

```csharp
public sealed class ChangeServiceLevelCommand(Booking booking, string newServiceLevel) : IBookingCommand
{
    private string? _priorServiceLevel;

    public void Execute()
    {
        _priorServiceLevel = booking.ServiceLevel;
        booking.ServiceLevel = newServiceLevel;
    }

    public void Undo()
    {
        if (_priorServiceLevel is null)
        {
            throw new InvalidOperationException("Undo was called before Execute.");
        }

        booking.ServiceLevel = _priorServiceLevel;
    }
}
```

The constructor captures the receiver, `booking`, and the new value,
`newServiceLevel`, but it does not yet know what the old value was, because
at construction time the command has not run and has no business reading
state it has not been asked to touch. `Execute` reads `booking.ServiceLevel`
into `_priorServiceLevel` before overwriting it, which is the one moment
the command can observe the value it is about to replace. `Undo` writes
that captured value straight back, and `ChangeServiceLevelCommandTests.Execute_then_undo_restores_the_prior_service_level`
proves the round trip directly: a booking starts at `"Standard"`, `Execute`
moves it to `"Express"`, and `Undo` returns it to `"Standard"`, asserted
both ways rather than trusted on inspection. `_priorServiceLevel` is
nullable specifically so `Undo` can detect the case where it runs before
`Execute` ever did, something that could otherwise silently write `null`
onto a booking's service level. `ChangeServiceLevelCommandTests.Undo_before_execute_throws_because_there_is_nothing_to_restore`
constructs a fresh command and calls `Undo` without ever calling `Execute`
first, and asserts an `InvalidOperationException`, catching a misuse that a
command queue running commands out of order, or a caller holding a command
reference it never ran, would otherwise turn into a confusing bug reported
somewhere else entirely. `ChangeWeightCommand` repeats the identical
discipline for `WeightKilograms`, snapshotting a nullable `decimal?` before
overwriting it and throwing the same way if `Undo` runs first.

`AddLineItemCommand` needs none of that machinery, and showing why is as
much the lesson as the snapshot pattern itself:

```csharp
public sealed class AddLineItemCommand(Booking booking, LineItem lineItem) : IBookingCommand
{
    public void Execute() => booking.AddLineItem(lineItem);

    public void Undo() => booking.RemoveLineItem(lineItem);
}
```

A line item is an immutable `LineItem` record the command already holds as
a constructor parameter, so there is nothing further to snapshot: the value
`Execute` adds is identical to the value `Undo` removes, and the command
never needs a private field to remember anything beyond what it was
constructed with. The difference between this command and
`ChangeServiceLevelCommand` is not a difference in how carefully each one
is written; it is a difference in what each one is reversing. Reversing an
assignment that overwrote a prior value requires remembering the value that
got overwritten. Reversing an addition to a collection requires remembering
only the thing that was added, which the command already has. A command
that mutates a property always needs an explicit `Execute`-time snapshot; a
command that appends an immutable value to a collection already holds its
own "snapshot" in the value it was handed at construction, with no further
field required.

## Delegates as commands

`IBookingCommand` is a one-method-pair interface, and a one-off amendment
that will never earn its own named class is a natural fit for the delegate
dissolution this book has used for Strategy and Template Method: two plain
`Action` values standing in for a hand-written implementation.
`DelegateCommand` is that form:

```csharp
public sealed class DelegateCommand(Action execute, Action undo) : IBookingCommand
{
    public void Execute() => execute();

    public void Undo() => undo();
}
```

Nothing about `DelegateCommand` knows or cares what `execute` and `undo`
actually do. A caller supplies two closures, and `DelegateCommand` is
nothing more than an `IBookingCommand` wrapper around whichever two
`Action` values it was constructed with, exactly the way `StrategyRateQuoter`'s
delegate-dissolved counterpart was nothing more than an
`Func<RateRequest, Money>` wrapper. `DelegateCommandTests.Delegate_command_executes_and_undoes_equivalently_to_the_interface_form`
builds the same service-level change two ways against two separate
bookings that start identical: once as a `ChangeServiceLevelCommand`, and
once as a `DelegateCommand` whose `execute` closure captures the prior
value into a local `string?` and whose `undo` closure writes that captured
value back. Both commands run, both bookings land on `"Express"`, both
commands undo, and both bookings return to `"Standard"`, with the test
asserting the two bookings agree with each other at every step rather than
only matching some expected literal. The closure pair does exactly what
`ChangeServiceLevelCommand`'s `_priorServiceLevel` field does, captures the
prior value at execution time in a variable the undo closure can read back
later, just without a dedicated class or a dedicated field to hold it; a
captured local plays the same role a private field plays in the named
class, visible only to the two closures that share the same enclosing
scope.

The trade here is the same one this book has drawn for Strategy and
Template Method: a named class gives a reviewer something to find by name,
something a test class can target on its own, and a type a dependency
injection container can register and resolve. A `DelegateCommand` gives up
all three of those in exchange for not needing a file, an interface
implementation, or a class declaration for a change that exists at exactly
one call site and will never be referred to by name in a planning
conversation. Promoting every one-off amendment in a real booking system to
its own `IBookingCommand` subclass produces a directory of single-use
classes that differ from each other only in which property they touch and
what value they touch it with; `DelegateCommand` collapses that entire
category into one reusable wrapper type, paying for the collapse only in
the readability a named class would have given a change that was never
going to be discussed by name regardless.

## The queue and its history

A single command executing and undoing in isolation proves the contract
works. A real amendment workflow needs more than one command applied
together, in a specific order, with a way to step backward through what
actually ran. `AmendmentQueue` holds three collections for exactly that:
a `Queue<IBookingCommand>` of commands waiting to run, a
`Stack<IBookingCommand>` of commands that have run, and a second stack of
commands that have been undone and could be redone.

```csharp
public sealed class AmendmentQueue
{
    private readonly Queue<IBookingCommand> _pending = new();
    private readonly Stack<IBookingCommand> _history = new();
    private readonly Stack<IBookingCommand> _undone = new();

    public void Enqueue(IBookingCommand command) => _pending.Enqueue(command);

    public void Run()
    {
        _undone.Clear();

        while (_pending.TryDequeue(out var command))
        {
            command.Execute();
            _history.Push(command);
        }
    }

    public void Undo()
    {
        if (!_history.TryPop(out var command))
        {
            throw new InvalidOperationException("No executed command to undo.");
        }

        command.Undo();
        _undone.Push(command);
    }
}
```

`Enqueue` only ever adds to `_pending`; nothing runs until `Run` is called.
`Run` drains the pending queue strictly in the order commands were
enqueued, a plain `Queue<T>` guarantee, applying each one and pushing it
onto `_history` immediately after it executes.
`AmendmentQueueTests.Run_executes_enqueued_commands_in_fifo_order` enqueues
a service-level change to `"Priority"`, then one to `"Express"`, then a
line-item addition, and asserts the booking ends at `"Express"`, the
last-enqueued service level, with a one-line comment explaining the
negative case directly: if the queue ran last-enqueued-first instead, the
booking would show `"Priority"`, so the assertion only passes under true
FIFO ordering. That framing matters because enqueue order and execution
order look identical in nearly every other test in the suite; this is the
one test built specifically to fail if the queue silently reversed its own
contract.

`Undo` is where the asymmetry between the two collections shows up.
Popping `_history`, a stack, means `Undo` always reverses the command that
ran most recently, regardless of when it was enqueued relative to the
others still sitting in history. Running three service-level changes in
order, `"Priority"`, then `"Express"`, then `"Overnight"`, and undoing
twice walks the booking back `"Overnight"` to `"Express"`, then `"Express"`
to `"Priority"`, exactly the reverse of the order the three commands ran
in. `AmendmentQueueTests.Undo_walks_the_executed_history_back_in_lifo_order`
asserts exactly that sequence, and the name is deliberate about the
distinction: `Run` is FIFO over the pending queue, and `Undo` is LIFO over
the executed history, and those are not the same order read in two
directions. A FIFO structure read backward from its tail would still
produce the earliest-enqueued item last; a LIFO stack produces the
most-recently-pushed item first by construction, which is the behavior
undo actually needs, since reversing the last thing that happened is
always the correct first step back, independent of when that last thing
was originally queued.

`Redo` is the third operation, and it exists because `Undo` throws
real information away if nothing captures it: once a command has been
reversed, re-applying it should not require the caller to reconstruct and
re-enqueue an equivalent command from scratch.

```csharp
public void Redo()
{
    if (!_undone.TryPop(out var command))
    {
        throw new InvalidOperationException("No undone command to redo.");
    }

    command.Execute();
    _history.Push(command);
}
```

`Undo` pushes the command it just reversed onto `_undone` as it pops it
from `_history`; `Redo` pops that same command back off `_undone`,
re-executes it, and pushes it back onto `_history`, which is why the same
command object can move between the two stacks any number of times without
ever being reconstructed. `AmendmentQueueTests.Redo_re_executes_the_most_recently_undone_command`
runs a single service-level change to `"Express"`, undoes it back to
`"Standard"`, then redoes it and confirms the booking is back at
`"Express"`. `Run` clears `_undone` at its own start, on the reasoning that
a fresh batch of commands applied against a booking invalidates whatever
was previously undone: redoing a change from before the new batch ran
could leave the booking in a state nothing in this queue's history ever
actually produced. Two guard tests close the gaps a caller could otherwise
hit at runtime rather than compile time:
`AmendmentQueueTests.Undo_with_no_executed_history_throws` calls `Undo` on
a brand-new queue with nothing ever run, and
`AsyncAmendmentQueueTests.UndoAsync_with_no_executed_history_throws` proves
the identical guard on the asynchronous counterpart later in this chapter;
both assert `InvalidOperationException` rather than a silent no-op, because
a queue that quietly does nothing when asked to undo something that never
happened hides a caller bug instead of surfacing it.

## The snapshot rule: value, not reference

`AmendmentQueueTests.Undo_restores_the_exact_snapshot_even_after_later_commands_changed_the_value`
is the test that earns its own section, because it proves a correctness
property that is easy to get wrong in a way that compiles cleanly and
passes every simpler test. The scenario: a booking starts at 10 kilograms,
a `ChangeWeightCommand` moves it to 20, a second `ChangeWeightCommand`
moves it to 30, both run through `AmendmentQueue.Run`. Undoing once should
bring the weight back to 20, the value immediately before the second
command ran, not back to 10, and undoing a second time should bring it
back to 10. The test asserts exactly that two-step sequence, and it passes
against `ChangeWeightCommand` precisely because of one design decision in
that command's `Execute` method: `_priorWeightKilograms = booking.WeightKilograms`
copies a `decimal` value into a private field at the moment `Execute` runs,
and `decimal` is a value type, so that assignment is a copy, not a
reference to anything that could change later.

The bug this test is built to catch is subtler than it first looks,
because it would not show up in `Undo_before_execute_throws_because_there_is_nothing_to_restore`
or in `Execute_then_undo_restores_the_prior_service_level`. It only shows
up once a second command runs against the same booking after the first
command has already captured its snapshot. Imagine a hypothetical command
that captured its snapshot as a reference into a shared mutable container
instead of copying the value out of it, for example a command holding a
reference to a single shared `decimal[]` cell that multiple commands write
through rather than each holding its own private field. The first
command's `Execute` would capture a reference to that cell rather than the
value inside it at that moment; by the time `Undo` runs, if a second
command has since written a new value into the same cell, the first
command's "snapshot" now reads whatever the second command left behind, not
the value that was actually there when the first command ran. The restored
weight would be wrong, and it would be wrong in a way no test that only
exercises a single command in isolation could ever catch, because the bug
only appears once two commands touch the same underlying storage in
sequence. `string` and `decimal`, the two types every snapshot in this
chapter's synchronous commands holds, are both immutable value-shaped
types in C#: a `string` reference, once obtained, can never be mutated out
from under a holder, because `string` itself has no mutating members, and a
`decimal` assignment always copies the sixteen-byte value rather than
aliasing it. Both snapshot fields in this example are safe by the nature
of the types involved, not merely by convention, but the discipline that
matters generalizes beyond those two types: a command's `Execute` method
must capture a value immune to being changed later by something outside
the command's own control, whether that immunity comes from a value type's
copy semantics, an immutable reference type like a `record`, or a defensive
copy taken explicitly where the underlying type is itself mutable. A
command that captured a reference to a mutable `List<T>` or a mutable
class instance as its "snapshot," instead of a copy of whatever state it
needed, would be vulnerable to exactly the stale-or-moved-on failure this
test is written to rule out, the moment any other code touched that same
mutable object between the command's `Execute` and its eventual `Undo`.

## Task-returning commands

Every command so far mutates an in-memory field and returns the instant it
is called. A real booking amendment frequently needs to confirm against
something outside the process, a carrier's booking API that has to agree
the new service level is valid before it takes effect, or a billing system
that has to record a charge before an amendment is considered final. That
confirmation is I/O-bound, which means the right return type for the
operation that performs it is `Task`, not `void`, and the right place to
wait on it is an `await`, not a blocking call that ties up a thread for the
round trip. `IAsyncBookingCommand` is `IBookingCommand`'s Task-returning
counterpart:

```csharp
public interface IAsyncBookingCommand
{
    Task ExecuteAsync(CancellationToken cancellationToken);

    Task UndoAsync(CancellationToken cancellationToken);
}
```

Both methods take a `CancellationToken` as a parameter rather than leaving
cancellation implicit, because a command library has no ambient context, no
request scope, and no UI thread to assume a cancellation source from; the
caller that starts an asynchronous amendment is the only party that can
decide when that amendment should stop waiting, and the only way for that
decision to reach the eventual I/O call several layers down is for every
method along the way to accept and forward the same token. `IAmendmentGateway`
stands in for the carrier's booking API this example never actually calls:

```csharp
public interface IAmendmentGateway
{
    Task ConfirmAsync(string bookingId, CancellationToken cancellationToken);

    Task RevertAsync(string bookingId, CancellationToken cancellationToken);
}
```

`ConfirmAmendmentCommand` is the one concrete `IAsyncBookingCommand` in the
example, and it does nothing beyond forward each call to the gateway it
was constructed with:

```csharp
public sealed class ConfirmAmendmentCommand(IAmendmentGateway gateway, string bookingId) : IAsyncBookingCommand
{
    public async Task ExecuteAsync(CancellationToken cancellationToken) =>
        await gateway.ConfirmAsync(bookingId, cancellationToken).ConfigureAwait(false);

    public async Task UndoAsync(CancellationToken cancellationToken) =>
        await gateway.RevertAsync(bookingId, cancellationToken).ConfigureAwait(false);
}
```

`ConfigureAwait(false)` on both awaits tells the awaited `Task` not to
resume its continuation on whatever synchronization context was captured
when the `await` started. Microsoft's own guidance on
`Task`-based asynchronous programming is explicit that library code, code
with no UI thread and no HTTP request context of its own to return to,
should use `ConfigureAwait(false)` on its awaits so the continuation can run
on whatever thread pool thread happens to be free rather than queuing back
onto a context that might not exist or might introduce unnecessary
contention. `ConfirmAmendmentCommand` is exactly that kind of code, a
reusable command with no caller-specific execution context of its own, so
every await inside it carries `ConfigureAwait(false)`. The
`cancellationToken` parameter on both methods is not swallowed or replaced
with `CancellationToken.None` anywhere in the forwarding chain; it is
threaded straight through to the matching gateway method, which means a
caller's cancellation request reaches the actual network call, not just the
command wrapper around it.

`AsyncAmendmentQueue` mirrors `AmendmentQueue` one method at a time, with
`Task` and `await` replacing the synchronous queue's direct calls:

```csharp
public sealed class AsyncAmendmentQueue
{
    private readonly Queue<IAsyncBookingCommand> _pending = new();
    private readonly Stack<IAsyncBookingCommand> _history = new();

    public void Enqueue(IAsyncBookingCommand command) => _pending.Enqueue(command);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (_pending.TryDequeue(out var command))
        {
            await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            _history.Push(command);
        }
    }

    public async Task UndoAsync(CancellationToken cancellationToken)
    {
        if (!_history.TryPop(out var command))
        {
            throw new InvalidOperationException("No executed command to undo.");
        }

        await command.UndoAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

`RunAsync` drains `_pending` in the identical FIFO order `Run` uses, but
awaits each command's `ExecuteAsync` before dequeuing the next one, so a
second command never starts confirming against the gateway until the first
one's confirmation has actually completed.
`AsyncAmendmentQueueTests.RunAsync_awaits_each_command_through_the_gateway_in_fifo_order`
enqueues two `ConfirmAmendmentCommand` instances against an
NSubstitute-stubbed `IAmendmentGateway`, awaits `RunAsync`, and then uses
NSubstitute's `Received.InOrder` to assert the gateway's `ConfirmAsync` was
called for the first booking before the second, proving the ordering
guarantee the same way the synchronous queue's FIFO test proves its own.
`AsyncAmendmentQueueTests.UndoAsync_reverses_the_most_recently_executed_command`
runs two confirmations and then calls `UndoAsync` once, asserting
`RevertAsync` was received for the second booking and never received at
all for the first, the same last-executed-first guarantee
`AmendmentQueue.Undo` makes, proven here against a stubbed collaborator
instead of a mutable field. `AsyncAmendmentQueueTests.Threads_the_callers_cancellation_token_through_confirm_and_revert`
builds its own `CancellationTokenSource`, passes its token into both
`RunAsync` and `UndoAsync`, and asserts the gateway's `ConfirmAsync` and
`RevertAsync` were each received with that exact token value, not
`Arg.Any<CancellationToken>()`, which is the test that would fail the
moment any method in the forwarding chain replaced the caller's token with
a different one instead of passing it straight through.

One structural difference between the two queues is deliberate rather than
an oversight: `AsyncAmendmentQueue` has no `Redo` and no second stack for
undone commands. Redoing a confirmed-then-reverted amendment against a real
carrier API means re-confirming it, a second network round trip against a
system that may have already reallocated the capacity the first
confirmation held, which is a meaningfully different operation from
popping a value back out of an in-memory stack. This example scopes
`AsyncAmendmentQueue` to run and single-step undo, the two operations its
test suite actually proves, rather than extending the synchronous queue's
full redo support to a gateway call where redo's correctness would depend
on guarantees the stand-in `IAmendmentGateway` interface never makes.

Both queue classes carry a `[SuppressMessage]` attribute for CA1711, the
Roslyn naming rule that flags an identifier ending in a suffix reserved for
a specific contract, in this case `Queue`, when the type is not itself an
implementation of that contract. `AmendmentQueue` and `AsyncAmendmentQueue`
are not `Queue<T>` subtypes and do not implement `IEnumerable<T>` or any
other collection interface; each one is a Command-pattern invoker that
happens to use a `Queue<T>` internally as one of its backing fields. The
suppression is scoped to that specific rule, on that specific type, with a
written justification explaining the name describes the domain concept, an
amendment queue a booking system's operators already call by that name, not
a claim that the type satisfies the `Queue<T>` contract.

{% include excalidraw.html name="14-async-command-undo" caption="Figure 14.2: The Task-returning command path. AsyncAmendmentQueue.RunAsync awaits each IAsyncBookingCommand.ExecuteAsync in FIFO order; ConfirmAmendmentCommand calls IAmendmentGateway.ConfirmAsync and RevertAsync with ConfigureAwait(false) on every await. UndoAsync walks the executed history back one command at a time, and the caller's CancellationToken threads unchanged from RunAsync and UndoAsync through to every gateway call beneath them." %}

## Build, run, observe

The example lives in
`examples/14-command-undo-and-task-returning-commands` and is
standalone-testable from its own directory, with no container anywhere in
the loop:

```sh
cd examples/14-command-undo-and-task-returning-commands
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. The suite splits across four files: two
`ChangeServiceLevelCommandTests` cases proving the execute-then-undo round
trip and the before-execute guard; five `AmendmentQueueTests` cases
covering FIFO execution, LIFO undo, the value-snapshot-under-later-mutation
proof, the empty-history guard, and redo; one `DelegateCommandTests` case
proving parity between the interface form and the closure form; and four
`AsyncAmendmentQueueTests` cases covering FIFO execution through a stubbed
gateway, last-executed-first undo, the empty-history guard on the async
path, and cancellation-token threading through both confirm and revert.
Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 12
  failed: 0
  succeeded: 12
  skipped: 0
  duration: 1s 564ms
```

## When not to use it

Command pays for itself when at least one of three things is actually
needed: undo, queued or batched application, or a log of what ran that
something else can replay later. A direct method call,
`booking.ServiceLevel = "Express"` or `carrier.Confirm(bookingId)`, is
cheaper to write, cheaper to read, and cheaper to debug than wrapping that
same call in a class with an `Execute` and an `Undo`, and it remains the
right choice for any change nothing ever needs to take back, queue behind
another change, or record for later replay. A settings screen that applies
a single toggle immediately, with no undo button and no batch of other
settings changes alongside it, does not need `IBookingCommand`; it needs
the one-line assignment the pattern exists to avoid when that assignment is
the whole story.

The cost of reaching for Command when none of those three needs is present
is concrete, not abstract. Every reversible command carries a private
field for its snapshot, a constructor that takes the receiver and the new
value, and an `Undo` method with its own guard clause against running
before `Execute`, three things a direct assignment statement never pays
for. A queue coordinating commands that nothing ever undoes or replays adds
a `Queue<T>` and a `Stack<T>` doing no useful work beyond what a `foreach`
loop over a plain list of actions would already do. The pattern's cost is
proportional to how much of its own machinery, undo, FIFO-versus-LIFO
ordering, replay, actually gets used; applied to a change with none of
those requirements, the machinery is pure overhead with nothing on the
other side of the ledger to offset it.

> **AI sidebar.** Asked to make a set of operations "support undo," an AI
> pairing partner will sometimes wrap every setter on a domain object in its
> own command class, including ones nothing in the request ever asks to
> reverse, producing a `ChangeDestinationCommand`, a `ChangeCarrierCommand`,
> and a dozen siblings for a system that only ever needed undo on the one
> or two fields a user can actually edit after submission. The opposite
> failure shows up just as often in generated undo code: a command that
> captures its snapshot as a reference to the same mutable object the
> receiver already holds, for example storing a reference to a receiver's
> own list instead of a copy of the values in it at that moment, which
> compiles cleanly, passes a test that only ever runs one command in
> isolation, and then silently fails to restore anything once a second
> command mutates that same shared list before the first command's `Undo`
> runs, exactly the bug this chapter's value-versus-reference snapshot test
> is built to catch. Check a generated command against two questions before
> accepting it: does the operation it wraps actually need undo, queuing, or
> replay, or would a direct call do, and does its snapshot hold a value
> immune to being changed by something else before `Undo` runs, or a live
> reference to the same mutable state the rest of the system keeps
> modifying.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of
  Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480)
  (Addison-Wesley, 1994): the original statement of the Command pattern,
  including the receiver, invoker, and undo-stack vocabulary this chapter's
  `AmendmentQueue` implements directly.
- Microsoft Learn, [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap):
  the authority for the `Task`-returning method shape `IAsyncBookingCommand`
  follows.
- Microsoft Learn, [`ConfigureAwait` FAQ](https://learn.microsoft.com/en-us/dotnet/standard/threading/configureawait-faq):
  the authority for using `ConfigureAwait(false)` in library code with no
  captured context worth returning to, the guidance
  `ConfirmAmendmentCommand` and `AsyncAmendmentQueue` follow on every await.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
