# 14-command-undo-and-task-returning-commands

Command as a reversible, queueable unit of work against a shared
`Booking`, in two forms:

- **Reversible, synchronous commands.** `IBookingCommand` declares `Execute`
  and `Undo`. `ChangeServiceLevelCommand` and `ChangeWeightCommand` each
  snapshot the prior value into a private field in `Execute`, then restore
  exactly that value in `Undo`, even after later commands have moved the
  booking further. `AddLineItemCommand` needs no separate snapshot: the
  `LineItem` it adds is itself the thing `Undo` removes. `DelegateCommand` is
  the delegate-as-command form, a plain `Action` pair standing in for a
  hand-written class.
- **A queue with undo history.** `AmendmentQueue.Enqueue` and `Run` apply
  commands first-in-first-out; `Run` pushes each executed command onto a
  history stack as it goes. `Undo` pops that stack and reverses the most
  recently executed command, one step at a time, last-executed-first.
  `Redo` re-applies the most recently undone one.
- **Task-returning commands.** `IAsyncBookingCommand` is the async
  counterpart: `ExecuteAsync` and `UndoAsync` return `Task`.
  `ConfirmAmendmentCommand` confirms or reverts an amendment against
  `IAmendmentGateway`, a stand-in for a carrier's booking API.
  `AsyncAmendmentQueue` mirrors `AmendmentQueue`: `RunAsync` awaits each
  queued command's `ExecuteAsync` in order, and `UndoAsync` awaits the most
  recently executed command's `UndoAsync`. Every await in the library is
  `ConfigureAwait(false)`, and the caller's `CancellationToken` is threaded
  through to the gateway call rather than left implicit.

**What the tests prove.** `ChangeServiceLevelCommandTests` executes then
undoes a single command and asserts the booking's service level returns to
its starting value. `AmendmentQueueTests` enqueues several commands and
asserts the FIFO result depends on enqueue order, not reverse order; chains
three service-level changes and undoes two of them to show history unwinds
last-executed-first; and chains three weight changes (10 to 20 to 30
kilograms) to prove each command restores the exact value it snapshotted,
not a live reference to a field another command has since moved on.
`DelegateCommandTests` builds the same change as both a named
`IBookingCommand` class and a `DelegateCommand` closure pair and asserts
both execute and undo identically. `AsyncAmendmentQueueTests` runs
`AsyncAmendmentQueue.RunAsync` against an NSubstitute-stubbed
`IAmendmentGateway`, asserts the gateway's `ConfirmAsync` is awaited once
per booking in enqueue order, asserts `UndoAsync` calls `RevertAsync` on the
most recently confirmed booking only, and asserts a caller-supplied
`CancellationToken` reaches the gateway unchanged through both directions.

Shipping domain, no containers: `Money` is a validating value type, `Booking`
is the plain mutable receiver every command targets, and `IAmendmentGateway`
is the one collaborator worth faking, since it is the only I/O in the
example.

## Build and test

```sh
cd examples/14-command-undo-and-task-returning-commands
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
