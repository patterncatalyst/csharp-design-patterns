---
title: "State and finite state machines"
order: 13
layout: tutorial
sectionid: docs
part: "selecting-varying-behavior"
description: "A real state machine and a probabilistic variant, with pattern matching reshaping both."
---

<!-- EXAMPLE (shipping domain): The consignment lifecycle as an FSM, plus a transit-delay model. -->

## Learning objectives

By the end of this chapter you can:

- Model a lifecycle as a finite state machine: a fixed set of states, a fixed set of events, and a transition table that names every legal (state, event) pair and rejects the rest.
- Write State as one switch expression over a tuple of two enums, say why the compiler wants a catch-all arm, and say when a class per state fits instead.
- Choose between throwing and returning a result for an illegal transition, and give a custom exception the three constructors CA1032 requires.
- Put a probabilistic transition behind `IRandomSource`, and say why a seeded source makes a simulation repeatable.
- Read a state machine's contract: which transitions are legal, which throw, and which draws produce which states.

## Why this matters

A consignment passes through fixed real-world events: it's loaded onto a vessel or truck, the vessel departs, customs clears it and a carrier signs for delivery. A tracking system must say at every point what the status is and which events are legal next. An undeparted consignment can't be delivered; a delivered one can't be loaded again. When the system reports an impossible status, billing, customer notifications and customs paperwork all inherit the error.

The version that invites those errors is a bag of flags: `IsLoaded`, `HasDeparted`, `IsCleared`, `IsDelivered`, each set by whichever handler gets the event. Nothing stops `HasDeparted` being true while `IsLoaded` is false. Each handler must remember which flags should already be set, and that convention lives nowhere a compiler or a test can check.

A finite state machine writes the lifecycle down: one type for the states, one for the events, and one piece of code that decides which pairs are legal. A second machine over the same states replaces one carrier event with a weighted draw, because not every transition in logistics is something a carrier reports.

{% include excalidraw.html name="13-state-and-finite-state-machines" caption="Figure 13.1: A consignment advances through its lifecycle on four carrier events, rejecting anything else; a probabilistic variant replaces one event with a weighted draw that can loop through a delay state." %}

## Pattern: State

### The problem

A consignment's behavior on an event depends on its current status, and only four of the possible (status, event) pairs are legal. Every other pair is a defect in whatever produced the event, and the machine must reject it at once rather than leave the consignment in a status nothing real would recognize.

### The solution

**State** lets an object change its behavior when its internal state changes, by putting the state-dependent decisions in one place instead of in flags scattered across handlers.

`ConsignmentStatus` has six members: `Booked`, `Loaded`, `InTransit`, `Cleared`, `Delivered` and `Delayed`. `HandlingEventType` has four: `Load`, `Depart`, `ClearCustoms` and `Deliver`. A `HandlingEvent` pairs an event type with a time and a place:

```csharp
public sealed record HandlingEvent(HandlingEventType Type, DateTimeOffset OccurredAt, string Location);
```

| From | Event | To |
| --- | --- | --- |
| `Booked` | `Load` | `Loaded` |
| `Loaded` | `Depart` | `InTransit` |
| `InTransit` | `ClearCustoms` | `Cleared` |
| `Cleared` | `Deliver` | `Delivered` |

### How C# models it

`ConsignmentStateMachine` is the context object a caller holds. It starts at `Booked`, the enum's default member, and keeps a `TrackingHistory`, an append-only list of `HandlingRecord` entries pairing each applied event with the status it produced. `Apply` is the one method that changes anything:

```csharp
public void Apply(HandlingEvent handlingEvent)
{
    ArgumentNullException.ThrowIfNull(handlingEvent);

    var resulting = Transitions.Next(CurrentStatus, handlingEvent.Type);
    CurrentStatus = resulting;
    History.Append(new HandlingRecord(handlingEvent, resulting));
}
```

`Apply` decides nothing. It asks `Transitions.Next`, assigns the result and records it. If `Next` throws, `CurrentStatus` and `History` are untouched, because the assignment and the append run only after it returns. A `Load` on a new machine gives `Loaded`; a `Deliver` on a new machine throws. Driving `Load`, `Depart`, `ClearCustoms` and `Deliver` ends at `Delivered` with four records, each with its resulting status. The flag version can't offer that ordered account, built by the same code that enforced the order.

### A switch expression in place of a class per state

The GoF form puts each state's behavior in its own class behind a common interface, so the object appears to change its class. C# reshapes the same intent into a pure function over a tuple:

```csharp
public static class Transitions
{
    public static ConsignmentStatus Next(ConsignmentStatus current, HandlingEventType evt) => (current, evt) switch
    {
        (ConsignmentStatus.Booked, HandlingEventType.Load) => ConsignmentStatus.Loaded,
        (ConsignmentStatus.Loaded, HandlingEventType.Depart) => ConsignmentStatus.InTransit,
        (ConsignmentStatus.InTransit, HandlingEventType.ClearCustoms) => ConsignmentStatus.Cleared,
        (ConsignmentStatus.Cleared, HandlingEventType.Deliver) => ConsignmentStatus.Delivered,
        _ => throw new InvalidTransitionException(
            $"Cannot apply '{evt}' while a consignment is '{current}'."),
    };
}
```

Every legal move sits in one method, read top to bottom. The `_ =>` arm is required. A tuple of two enums has 24 combinations, six statuses times four events, and only four are named. Without the catch-all the compiler reports CS8509, "the switch expression does not handle all possible values of its input type." The arm is also the main guarantee: an unnamed pair throws `InvalidTransitionException`. An arm that returned `current` unchanged would let a `Deliver` on a `Booked` consignment do nothing, which is worse than rejecting it, because nothing in the resulting state shows that anything went wrong.

{% include codetabs.html langs="Switch expression|Class per state" %}
```csharp
public static class Transitions
{
    public static ConsignmentStatus Next(ConsignmentStatus current, HandlingEventType evt) => (current, evt) switch
    {
        (ConsignmentStatus.Booked, HandlingEventType.Load) => ConsignmentStatus.Loaded,
        (ConsignmentStatus.Loaded, HandlingEventType.Depart) => ConsignmentStatus.InTransit,
        _ => throw new InvalidTransitionException(
            $"Cannot apply '{evt}' while a consignment is '{current}'."),
    };
}
```
```csharp
public interface IConsignmentState
{
    ConsignmentStatus Status { get; }
    IConsignmentState Handle(HandlingEventType evt);
}

public sealed class BookedState : IConsignmentState
{
    public ConsignmentStatus Status => ConsignmentStatus.Booked;

    public IConsignmentState Handle(HandlingEventType evt) => evt switch
    {
        HandlingEventType.Load => new LoadedState(),
        _ => throw new InvalidTransitionException(
            $"Cannot apply '{evt}' while a consignment is 'Booked'."),
    };
}
```

The switch carries all four moves as data read in one pass; the class form spreads them across one file per state, each seeing only its own corner. A class per state is the better tool when a state carries more than a position:

- **Per-state data.** `InTransit` needs a count of customs inspections, a list of ports visited or a timer for time in status.
- **Per-state behavior a consumer calls.** Code asks a state "do you still accept handling events?" through an interface method instead of comparing statuses.

Neither holds here. Every status is a label, and the only question is what an event produces from it.

### Illegal transitions: throw or return a result

`InvalidTransitionException` is sealed and has the three constructors CA1032 requires: parameterless, message, and message plus inner exception.

```csharp
public sealed class InvalidTransitionException : Exception
{
    public InvalidTransitionException()
    {
    }

    public InvalidTransitionException(string message)
        : base(message)
    {
    }

    public InvalidTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

| Signal | Fits when |
| --- | --- |
| Throw `InvalidTransitionException` | An illegal transition is a defect upstream: a `Deliver` reaching a `Booked` consignment means something generated or replayed events out of order. Stop the call with a typed failure. |
| Return a result, such as a `TransitionResult` holding the new status or a reason | Rejection is an expected branch: a bulk import validating replayed events wants every failure collected, and the caller pattern-matches instead of wrapping each call in `try`/`catch`. |

A result-returning overload could sit beside `Transitions.Next` without disturbing it. Validating untrusted or replayed event streams at scale is the clearest case for it.

### A probabilistic transit-delay variant

Whether a consignment in transit clears on schedule or is delayed is modeled as a weighted draw. `ProbabilisticTransitMachine` steps over the same `ConsignmentStatus` set:

```csharp
public ConsignmentStatus Step(ConsignmentStatus current) => current switch
{
    ConsignmentStatus.Booked => ConsignmentStatus.Loaded,
    ConsignmentStatus.Loaded => ConsignmentStatus.InTransit,
    ConsignmentStatus.InTransit => _source.NextDouble() < _delayProbability
        ? ConsignmentStatus.Delayed
        : ConsignmentStatus.Cleared,
    ConsignmentStatus.Delayed => ConsignmentStatus.InTransit,
    ConsignmentStatus.Cleared => ConsignmentStatus.Delivered,
    ConsignmentStatus.Delivered => ConsignmentStatus.Delivered,
    _ => throw new ArgumentOutOfRangeException(nameof(current), current, "Unknown consignment status."),
};
```

Every state but `InTransit` progresses deterministically, and `Delivered` maps to itself, so further steps change nothing. From `InTransit`, a draw from an injected `IRandomSource` below `delayProbability` gives `Delayed`; at or above it gives `Cleared`. `Delayed` maps back to `InTransit`, so the next step retries the draw. `Simulate` runs `Step` from `Booked` and returns the path including the start, so twenty steps give twenty-one statuses.

Randomness sits behind an interface so outcomes can be pinned. `IRandomSource` has one method, `NextDouble`, and `SeededRandomSource` wraps a `System.Random` built from a caller's seed:

- **Same seed.** Two machines on `SeededRandomSource(42)` walk identical twenty-step paths; seed `7` gives a different one.
- **Scripted draws.** An NSubstitute `IRandomSource` returning `0.1` against a probability of `0.5` gives `Delayed`; returning `0.5` gives `Cleared`, so the boundary is inclusive on the clearing side.

`SeededRandomSource` suppresses CA5394, which flags `System.Random` as insecure randomness, in one method:

```csharp
public double NextDouble()
{
#pragma warning disable CA5394
    return _random.NextDouble();
#pragma warning restore CA5394
}
```

The justification sits beside it: the draw drives a delay simulation, nothing derives a token, key or password from it, and the point of `IRandomSource` is that a test can substitute it. A cryptographically secure generator would make the sequence unseedable, for no benefit. That reasoning is what CA5394 asks a reviewer to confirm, and it belongs next to the suppression, where it travels with the file.

{% include excalidraw.html name="13-probabilistic-transit-model" caption="Figure 13.2: A weighted draw decides whether a consignment in transit clears or loops back through a delay state; a seeded random source lets two machines replay the identical sequence." %}

## When not to use it

- **One real transition.** A consignment tracked only as delivered or not has no illegal moves to guard beyond setting a flag twice. A boolean does; a transition table, a custom exception and a history are ceremony without a rule behind them.
- **A process the state set can't express.** Returns after delivery, mid-transit re-routing or split shipments with their own statuses need those states and events in the table. Forcing them through the four-event happy path rejects legal operations as `InvalidTransitionException`. Add the missing states and events; don't loosen `Transitions.Next` until the exception stops firing.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a transition.** "Add a `Cancelled` status" leaves open which statuses can be cancelled, whether to add classes or switch arms, and what an illegal pair does. State them:

> Add `Cancelled` to `ConsignmentStatus` and `Cancel` to `HandlingEventType`. In `Transitions.Next`, add `(Booked, Cancel)` and `(Loaded, Cancel)` arms that return `Cancelled`, and keep the `_ =>` arm throwing `InvalidTransitionException`. Don't add state classes, and don't suppress CS8509.

Check that every arm is a pair the domain allows and that the catch-all still throws.
</blockquote>

## Build, run, observe

```sh
cd examples/13-state-and-finite-state-machines
dotnet test
```

All 9 tests pass, and `scripts/verify.sh` runs them with every other example. They cover the transition table, the context object advancing, rejecting and recording, seeded replay and divergence, and the scripted draw on each side of the threshold.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of the State pattern, including the class-per-state shape a switch expression replaces for a lifecycle with no per-state fields.
- Microsoft Learn, [Switch expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression): switch-expression exhaustiveness checking and the CS8509 warning a missing catch-all arm produces.
- Microsoft Learn, [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching): tuple patterns over multiple values, the shape `Transitions.Next` uses to match `(ConsignmentStatus, HandlingEventType)` pairs.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
