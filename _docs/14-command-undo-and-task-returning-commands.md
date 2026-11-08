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

- Describe Command as a reified operation, an object standing in for a method call, and say why that makes undo and queuing possible.
- Implement a reversible `IBookingCommand` whose `Execute` snapshots the prior state and whose `Undo` restores it.
- Write `DelegateCommand`, the delegate form, and match a hand-written command's execute and undo.
- Build an `AmendmentQueue` that runs commands first in, first out and undoes them last in, first out.
- Apply the snapshot rule: capture the prior value, never a live reference that a later command can change.
- Write a Task-returning command that awaits a gateway with `ConfigureAwait(false)` and a threaded `CancellationToken`, and say what changes between the synchronous and asynchronous queues.

## Why this matters

A customer bumps a shipment from standard to express, then asks to go back twenty minutes later. A clerk batches three changes to one booking, a service level, a weight correction and a rebooking-fee line item, and wants to pull the third if it's wrong. A reconciliation job replays amendments against a nightly snapshot and must take one back out without rebuilding the batch.

`booking.ServiceLevel = "Express"` serves none of these. It's a statement, not something a program can hold, queue or reverse. Once it runs, only code that already knew the old value can restore it, so every caller that might undo a change shadows the booking's state by hand. Three amendments are three unrelated statements with no record of order.

{% include excalidraw.html name="14-command-undo-and-task-returning-commands" caption="Figure 14.1: A booking amendment reified as a command object instead of a direct mutation, each one snapshotting what its own undo needs; a queue runs commands FIFO and unwinds its history LIFO." %}

## Pattern: Command

### The problem

Booking amendments of unrelated kinds must be queued together, applied in order and reversed one at a time. The code that queues and reverses them shouldn't know which concrete change each one is.

### The solution

A **Command** turns a request into an object that can apply itself and reverse itself, so requests can be queued, logged and undone through one uniform shape.

A `ChangeServiceLevelCommand` and an `AddLineItemCommand` do unrelated things to a `Booking`, but a queue treats them the same: enqueue, run, undo. The receiver is a plain mutable `Booking` with a `BookingId`, a settable `ServiceLevel`, a settable `WeightKilograms`, and a read-only view of line items changed through `AddLineItem` and `RemoveLineItem`.

### How C# models it

The contract is two methods:

```csharp
public interface IBookingCommand
{
    void Execute();

    void Undo();
}
```

Anything that can apply and reverse itself qualifies. A property change needs a snapshot:

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

The constructor holds the receiver and the new value but doesn't read the old one, because the command hasn't run. `Execute` reads `booking.ServiceLevel` into `_priorServiceLevel` just before overwriting it, the one moment the old value is visible, and `Undo` writes it back: `"Standard"` goes to `"Express"` and back to `"Standard"`. The field is nullable so that `Undo` before `Execute` throws `InvalidOperationException` instead of writing `null` onto the booking, a misuse an out-of-order queue or a never-run command would otherwise turn into a bug reported somewhere else. `ChangeWeightCommand` does the same with a `decimal?`.

A collection addition needs no snapshot:

```csharp
public sealed class AddLineItemCommand(Booking booking, LineItem lineItem) : IBookingCommand
{
    public void Execute() => booking.AddLineItem(lineItem);

    public void Undo() => booking.RemoveLineItem(lineItem);
}
```

`LineItem` is an immutable record the command already holds, so the value `Execute` adds is the value `Undo` removes.

| Command reverses | Needs |
| --- | --- |
| An assignment that overwrote a value | The overwritten value, captured in `Execute` |
| An addition of an immutable value to a collection | Only the value, already held from construction |

### Delegates as commands

A one-off amendment that doesn't warrant a named class is two `Action` values:

```csharp
public sealed class DelegateCommand(Action execute, Action undo) : IBookingCommand
{
    public void Execute() => execute();

    public void Undo() => undo();
}
```

`DelegateCommand` is a wrapper around whichever closures it gets. A service-level change built as a `ChangeServiceLevelCommand` and as a `DelegateCommand`, whose `execute` captures the prior value in a local `string?` and whose `undo` writes it back, leaves two identical bookings in agreement at every step. The captured local plays the role of the private field, visible only to the two closures that share its scope.

The trade is the usual one. A named class gives a reviewer something to find by name, a target for its own tests and a type a container can register. `DelegateCommand` gives those up for not needing a file for a change at one call site that nobody will discuss by name. Without it, a booking system grows a directory of single-use command classes that differ only in which property they touch.

### The queue and its history

`AmendmentQueue` holds a `Queue<IBookingCommand>` of pending commands, a `Stack<IBookingCommand>` of executed ones and a second stack of undone ones:

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

Nothing runs until `Run`, which drains the pending queue in enqueue order and pushes each command onto `_history`. Enqueueing changes to `"Priority"`, then `"Express"`, then a line item leaves the booking at `"Express"`; last-in-first-out execution would leave `"Priority"`.

`Undo` pops `_history`, so it always reverses the most recent command. Running `"Priority"`, `"Express"`, `"Overnight"` and undoing twice walks back to `"Express"`, then `"Priority"`. These are two structures, not one order read in two directions: `Run` is FIFO over the pending queue, `Undo` is LIFO over the executed history, and reversing the last thing that happened is always the right first step back.

`Redo` keeps what `Undo` would otherwise throw away:

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

The same command object moves between `_history` and `_undone` any number of times without being rebuilt: `"Express"` undone to `"Standard"` and redone is `"Express"` again. `Run` clears `_undone` first, because redoing a change from before a new batch could produce a state no history ever held. `Undo` on an empty history throws `InvalidOperationException` in both queues; a silent no-op would hide a caller bug.

### The snapshot rule: value, not reference

A booking starts at 10 kilograms. One `ChangeWeightCommand` moves it to 20, a second to 30, and both run. One undo must give 20, the value just before the second command, and a second undo 10. That holds because `_priorWeightKilograms = booking.WeightKilograms` copies a `decimal`, a value type, at the moment `Execute` runs.

The failure this rules out shows up only when a second command touches the same storage after the first has captured its snapshot. A command that held a reference to a shared mutable cell, say one `decimal[]` element that several commands write through, would capture the cell instead of its value. By the time `Undo` runs, the cell holds whatever the second command wrote, and the restored weight is wrong. A single command in isolation can't reveal it.

`string` and `decimal`, the two snapshot types here, are safe by their nature: `string` has no mutating members and a `decimal` assignment copies the value. The rule generalizes: `Execute` must capture something nothing outside the command can change later.

- **A value type**, copied on assignment.
- **An immutable reference type**, such as a `record`.
- **A defensive copy**, where the underlying type is mutable. A command that keeps a reference to a mutable `List<T>` or class instance as its snapshot fails as soon as other code touches that object before `Undo`.

### Task-returning commands

A real amendment often confirms against something outside the process: a carrier's booking API or a billing system. That's I/O-bound, so the operation returns `Task` and the caller awaits it instead of blocking a thread:

```csharp
public interface IAsyncBookingCommand
{
    Task ExecuteAsync(CancellationToken cancellationToken);

    Task UndoAsync(CancellationToken cancellationToken);
}
```

Both methods take a `CancellationToken`. A command library has no ambient request scope or UI thread to take cancellation from; only the caller can decide when to stop waiting, and the decision reaches the I/O call only if every method on the way forwards the token. `IAmendmentGateway` stands in for the carrier API:

```csharp
public interface IAmendmentGateway
{
    Task ConfirmAsync(string bookingId, CancellationToken cancellationToken);

    Task RevertAsync(string bookingId, CancellationToken cancellationToken);
}
```

```csharp
public sealed class ConfirmAmendmentCommand(IAmendmentGateway gateway, string bookingId) : IAsyncBookingCommand
{
    public async Task ExecuteAsync(CancellationToken cancellationToken) =>
        await gateway.ConfirmAsync(bookingId, cancellationToken).ConfigureAwait(false);

    public async Task UndoAsync(CancellationToken cancellationToken) =>
        await gateway.RevertAsync(bookingId, cancellationToken).ConfigureAwait(false);
}
```

`ConfigureAwait(false)` means the continuation doesn't resume on the captured synchronization context. The [`ConfigureAwait` FAQ](https://learn.microsoft.com/en-us/dotnet/standard/threading/configureawait-faq) advises it for library code with no UI thread or request context to return to, so the continuation runs on any free thread-pool thread. The token is never replaced with `CancellationToken.None`, so a caller's cancellation reaches the network call, not just the wrapper.

`AsyncAmendmentQueue` mirrors the synchronous queue with `await` in place of direct calls:

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

`RunAsync` awaits each `ExecuteAsync` before dequeuing the next, so a second confirmation never starts before the first completes. With two confirmations, NSubstitute's `Received.InOrder` sees `ConfirmAsync` for the first booking before the second. One `UndoAsync` reverts the second booking and never the first. A caller's own token arrives unchanged at both `ConfirmAsync` and `RevertAsync`.

| | `AmendmentQueue` | `AsyncAmendmentQueue` |
| --- | --- | --- |
| Run order | FIFO | FIFO, each command awaited before the next |
| Undo order | LIFO | LIFO |
| Redo | Yes | No |
| Cancellation | None | Caller's token forwarded to the gateway |

The asynchronous queue has no `Redo` by design. Redoing a reverted amendment against a carrier means confirming again, a second round trip to a system that may have reallocated the capacity, which is a different operation from popping an in-memory stack. The stand-in gateway makes no guarantee that would make redo correct.

Both queues suppress CA1711, which flags a name ending in a reserved suffix such as `Queue` on a type that doesn't implement that contract. Neither is a `Queue<T>` subtype or implements a collection interface; each is a Command invoker that uses a `Queue<T>` internally. The suppression is scoped to that rule on that type, with a justification: the name is the domain concept operators already use.

{% include excalidraw.html name="14-async-command-undo" caption="Figure 14.2: The Task-returning command path awaits each command through a gateway in FIFO order, unwinds history one step at a time, and threads the caller's cancellation token through every call." %}

## When not to use it

Command pays for itself when something needs undo, queued or batched application, or a log that can be replayed. Without one of those:

- **A direct call is cheaper.** `booking.ServiceLevel = "Express"` or `carrier.Confirm(bookingId)` is easier to write, read and debug. A settings toggle applied at once, with no undo and no batch, is one assignment.
- **The machinery is overhead.** Each reversible command carries a snapshot field, a constructor taking receiver and value, and an `Undo` with its own guard. A queue whose commands are never undone or replayed is a `Queue<T>` and a `Stack<T>` doing what a `foreach` over a list of actions does.

<blockquote class="ai-sidebar" markdown="1">
**Specifying undo.** "Make these operations support undo" leaves open which operations need it and what each command captures. State both:

> Add undo only for the service-level and weight changes, as `ChangeServiceLevelCommand` and `ChangeWeightCommand` implementing `IBookingCommand`. Each `Execute` copies the prior value into a private nullable field before overwriting it, and `Undo` throws `InvalidOperationException` if `Execute` never ran. Don't capture a reference to a mutable list or object as a snapshot; copy the value.

Check each snapshot field: a value, an immutable type or a copy, never a live reference.
</blockquote>

## Build, run, observe

```sh
cd examples/14-command-undo-and-task-returning-commands
dotnet test
```

All 12 tests pass, and `scripts/verify.sh` runs them with every other example. They cover the execute and undo round trip, FIFO execution, LIFO undo, the snapshot under later mutation, redo, the empty-history guards, delegate parity, and token threading through the asynchronous queue.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of
  Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480)
  (Addison-Wesley, 1994): the original statement of the Command pattern,
  including the receiver, invoker, and undo-stack vocabulary `AmendmentQueue`
  implements.
- Microsoft Learn, [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap):
  the `Task`-returning method shape `IAsyncBookingCommand` follows.
- Microsoft Learn, [`ConfigureAwait` FAQ](https://learn.microsoft.com/en-us/dotnet/standard/threading/configureawait-faq):
  using `ConfigureAwait(false)` in library code with no captured context
  worth returning to, the guidance `ConfirmAmendmentCommand` and
  `AsyncAmendmentQueue` follow on every await.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
