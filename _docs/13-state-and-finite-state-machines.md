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

- Model a lifecycle as a finite state machine: a fixed set of states, a fixed
  set of events, and a transition table naming every legal (state, event)
  pair while rejecting everything else.
- Write the GoF State pattern as a single switch expression over a tuple of
  two enums, explain why the compiler demands a catch-all arm for
  exhaustiveness, and say when a class-per-state hierarchy is the right tool
  instead.
- Choose between throwing a custom exception and returning a result value for
  an illegal transition, and implement the three constructors a custom
  exception type needs to satisfy CA1032.
- Build a probabilistic state transition behind an `IRandomSource`
  abstraction, and explain why a seeded source makes a simulation
  repeatable without weakening the randomness anywhere it actually matters.
- Read a state machine's test suite as proof of its contract: which
  transitions are legal, which throw, and which sequence of draws produces
  which sequence of states.

## Why this matters

A consignment moving from a warehouse to a buyer's dock passes through a
fixed sequence of real-world events: someone loads it onto a vessel or
truck, the vessel departs, customs clears it at the border, and a carrier
signs for delivery at the far end. A tracking system has to answer one
question correctly at every point in that journey: what is this
consignment's status right now, and which events are even legal to apply to
it next. A consignment that has not yet departed cannot be marked delivered.
A consignment already delivered cannot be loaded again. Those are not edge
cases a team discovers in production; they are the entire reason a tracking
system exists, because the moment it reports a wrong or impossible status,
every downstream system that trusts that status, billing, customer
notifications, customs paperwork, inherits the error.

The version of this system that invites those errors is the one built
around a bag of boolean flags: `IsLoaded`, `HasDeparted`, `IsCleared`,
`IsDelivered`, each one set independently by whatever code path happens to
handle a given event. Nothing stops two flags from disagreeing with each
other. A consignment can end up with `HasDeparted` true and `IsLoaded`
false, a combination that describes nothing real, because no single piece of
code owns the question of which combinations are even possible. Every new
event handler that sets one of these flags has to know, by convention and by
memory, which other flags should already be true before it runs, and that
convention lives nowhere the compiler or a test can check it against. A
reviewer reading one handler in isolation cannot tell whether it is being
called at a legal point in the lifecycle or not, because the lifecycle
itself was never written down as a single artifact anyone could check
against.

A finite state machine names the lifecycle explicitly instead of
implying it across a scattered set of flags. One type enumerates every
state a consignment can occupy. A second type enumerates every event a
carrier can report. A third piece of code, and only that piece, decides
which (state, event) pairs are legal and what each one produces, with
everything not on that list rejected rather than silently accepted. The
example behind this chapter, in
`examples/13-state-and-finite-state-machines`, builds exactly that
machine for a consignment's handling lifecycle, and then builds a second
machine over the identical set of states where one exit is a coin flip
instead of a carrier event, because not every real transition in a
logistics system is driven by something a carrier chooses to report.

{% include excalidraw.html name="13-state-and-finite-state-machines" caption="Figure 13.1: The consignment lifecycle as a finite state machine. Transitions.Next advances Booked through Loaded, InTransit, Cleared, and Delivered on the four carrier events it recognizes, throwing InvalidTransitionException on anything else; ProbabilisticTransitMachine drives the same state set but replaces the InTransit-to-Cleared carrier event with a weighted draw that can instead loop through Delayed and back to InTransit." %}

## The consignment lifecycle as a finite state machine

`ConsignmentStatus` is the complete state set: `Booked`, `Loaded`,
`InTransit`, `Cleared`, `Delivered`, and `Delayed`. `HandlingEventType` is
the complete event set a carrier reports against a consignment: `Load`,
`Depart`, `ClearCustoms`, and `Deliver`. A `HandlingEvent` record pairs one
of those event types with when and where it happened:

```csharp
public sealed record HandlingEvent(HandlingEventType Type, DateTimeOffset OccurredAt, string Location);
```

Four of the thirty possible (status, event) combinations are legal: a
`Booked` consignment that is `Load`ed becomes `Loaded`; a `Loaded`
consignment that `Depart`s becomes `InTransit`; an `InTransit` consignment
that clears customs becomes `Cleared`; a `Cleared` consignment that is
delivered becomes `Delivered`. Every other combination, delivering a booked
consignment, loading one that already departed, clearing customs twice, is
not a state this system models; it is a defect in whatever caller produced
that event, and the machine's job is to say so immediately rather than
accept the event and leave the consignment in a status nothing in the real
world would recognize.

`ConsignmentStateMachine` is the State pattern's context object: the thing
a caller actually holds and calls into, with no branching logic of its own.
It tracks the current `ConsignmentStatus`, starting at `Booked`, the enum's
default member, because every consignment begins booked before anything
happens to it. It also tracks a `TrackingHistory`, an append-only list of
`HandlingRecord` entries, each one pairing the `HandlingEvent` that was
applied with the `ConsignmentStatus` it produced. `Apply` is the one
public method that changes anything:

```csharp
public void Apply(HandlingEvent handlingEvent)
{
    ArgumentNullException.ThrowIfNull(handlingEvent);

    var resulting = Transitions.Next(CurrentStatus, handlingEvent.Type);
    CurrentStatus = resulting;
    History.Append(new HandlingRecord(handlingEvent, resulting));
}
```

Three lines, and none of them decide which transitions are legal.
`ConsignmentStateMachine` asks `Transitions.Next` for the answer, applies
whatever it gets back, and records it. If `Transitions.Next` throws, `Apply`
throws too, with `CurrentStatus` and `History` both left untouched, because
the assignment and the append only run after the call returns successfully.
`ConsignmentStateMachineTests.Apply_with_Load_from_Booked_transitions_to_Loaded`
confirms the common path: a freshly constructed machine, handed a `Load`
event, reports `CurrentStatus` as `Loaded`.
`ConsignmentStateMachineTests.Applying_an_illegal_event_throws_InvalidTransitionException`
confirms the rejection path: the same freshly constructed machine, handed a
`Deliver` event with nothing loaded or departed yet, throws rather than
silently recording an impossible delivery.
`ConsignmentStateMachineTests.Full_lifecycle_drives_Booked_through_Delivered_and_records_history`
drives all four legal events in order, `Load`, `Depart`, `ClearCustoms`,
`Deliver`, and asserts both that `CurrentStatus` ends at `Delivered` and
that `History.Records` holds all four steps with the right resulting status
recorded at each one. That last assertion is what the flag-based version of
this system cannot offer: a single, ordered, append-only account of exactly
what happened to this consignment and in what sequence, built by the same
code path that enforced which sequence was legal in the first place.

## A switch expression in place of a class per state

The Gang of Four describe State as a pattern that lets an object appear to
change its class as its internal state changes, with each state's behavior
captured in its own class behind a common interface. That is the heavier
answer to the same problem this example solves, one `IConsignmentState`
implementation per `ConsignmentStatus`, each one holding whatever behavior
is specific to that status and handling the question "what happens if this
event arrives while I'm active." The modern C# answer reshapes that same
intent into a pure function: a switch expression over a tuple of the
current status and the incoming event, with every legal pair as one arm and
nothing else to maintain:

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

Every legal move in the entire lifecycle sits in one method, readable top
to bottom in a single pass, with no need to open six class files to
reconstruct what the whole state machine does. That last arm, `_ =>`, is
not optional decoration. The compiler checks a switch expression's patterns
for exhaustiveness, and a tuple of two enums has thirty possible value
combinations, six statuses times four events known at compile time, only
four of which appear as named patterns above. Leaving the catch-all off
produces warning CS8509, "the switch expression does not handle all
possible values of its input type," because the compiler has no pattern
covering the other twenty-six combinations and no way to know what a
caller should get back for them. The catch-all arm is also where this
example's main correctness guarantee lives: a `(current, evt)` pair that
has no explicit arm did not fall through to some default status by
accident, it threw, because `_ =>` throws `InvalidTransitionException`
rather than returning some placeholder `ConsignmentStatus`. A switch
expression that reached the default case by returning `current` unchanged,
instead of throwing, would quietly let a `Deliver` event on a `Booked`
consignment do nothing, which is worse than rejecting it, because nothing
in the resulting state signals that anything went wrong.

A class-per-state hierarchy is the right tool once a state carries more
than a position in a sequence, a point this chapter's companion
walk through template method variation makes about inheritance generally.
If `InTransit` needed its own accumulated data, a running count of customs
inspections attempted, a list of ports already visited, a timer tracking
how long the consignment has been in that status, a class gives that state
a natural home for fields a switch arm has nowhere to put. If some other
part of the system needed to ask "is this state one that still accepts new
handling events" and branch on the answer through an interface method
rather than a status comparison, a class-per-state design gives that
consumer something to call. Neither condition holds for this lifecycle:
every status here is purely a label, nothing more than a position the
consignment currently occupies, and the only question ever asked of it is
"what does this event produce from here," which a switch expression
answers in one line per legal pair. Promoting that into six classes
implementing a shared interface would move the same four facts into six
files without attaching any new behavior or data to any of them.

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

Set side by side, the difference is the same one Strategy's dissolve to a
delegate draws: the switch expression form carries all four legal moves as
data read in one pass, while the class form spreads the identical four
moves across however many state classes exist, with each class only ever
seeing its own corner of the table. Thirty lines become six small files
with nothing shared between them except the interface signature. For a
lifecycle this size, with no per-state fields and no external consumer
treating a state as a type, the function form is the one that costs less
to read and less to extend.

## Illegal transitions: throw or return a result

`Transitions.Next` signals an illegal pair by throwing
`InvalidTransitionException`, a sealed exception type with the three
constructors a custom exception needs to satisfy the CA1032 analyzer rule:
a parameterless constructor, a constructor taking only a message, and a
constructor taking a message and an inner exception.

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

Throwing fits this lifecycle because an illegal transition here is a
programming defect somewhere upstream, not a routine outcome a caller is
expected to branch on. A `Deliver` event reaching a `Booked` consignment
means some other part of the system generated or replayed an event out of
order, and the right response is to stop that call immediately with a
specific, typed failure a test can assert against with
`Assert.Throws<InvalidTransitionException>`, exactly what
`TransitionsTests.Booked_and_Deliver_throws_InvalidTransitionException` and
`ConsignmentStateMachineTests.Applying_an_illegal_event_throws_InvalidTransitionException`
both do.

A result-type alternative is the better fit once illegal transitions stop
being exceptional and start being an expected branch of ordinary control
flow, for example a bulk import validating a batch of replayed handling
events before applying any of them, where most consignments are expected
to produce at least one rejected event and a caller wants to collect every
failure rather than stop at the first one. That shape returns something
like a discriminated union or a small result record, a `TransitionResult`
carrying either the new `ConsignmentStatus` or a reason it could not be
reached, and lets a caller pattern-match on the outcome instead of wrapping
every call in a `try`/`catch`. Nothing about `Transitions.Next`'s signature
forces the throwing choice; a second overload returning a result type could
sit next to it without disturbing the one this example ships, and a team
validating untrusted or replayed event streams at scale is the clearest
case for reaching for that second overload instead of the exception this
chapter's example throws.

## A probabilistic transit-delay variant

Not every real transition in a logistics system waits for a carrier to
report an event. Whether a consignment in transit clears on schedule or
gets delayed is, at the level this example models it, closer to a weighted
coin flip than to a deterministic rule, and `ProbabilisticTransitMachine`
drives exactly that kind of exit over the same `ConsignmentStatus` set
`Transitions` uses for the event-driven machine:

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

`Booked`, `Loaded`, `Cleared`, and `Delivered` all progress deterministically,
the same shape `Transitions.Next` uses for its own legal arms.
`Delivered` is a terminal state that maps to itself, so repeated calls once
a consignment arrives stop changing anything. The one state with a real
branch is `InTransit`: a draw from an injected `IRandomSource`, compared
against a configured `delayProbability`, sends the consignment to `Delayed`
below that threshold and to `Cleared` at or above it. A delayed consignment
is not stuck; `Delayed` maps back to `InTransit`, so the next `Step` call
retries the same probabilistic exit rather than leaving the consignment
parked in a dead end. `Simulate` runs that `Step` function repeatedly from
`Booked`, returning the full path including the starting status, so a
caller asking for twenty steps gets twenty-one statuses back.

Randomness lives behind an interface here rather than being called directly
because a test that depends on an unseeded `System.Random` has no way to
assert a specific outcome without either mocking something or accepting
flakiness. `IRandomSource` exposes exactly the one method this machine
needs, `NextDouble`, and `SeededRandomSource` wraps a `System.Random`
constructed from a caller-supplied seed, so two machines built on the same
seed draw the identical sequence of values and therefore walk the identical
sequence of statuses.
`ProbabilisticTransitMachineTests.The_same_seed_produces_an_identical_status_sequence`
builds two machines on `SeededRandomSource(42)` and asserts their twenty-step
`Simulate` paths are equal;
`ProbabilisticTransitMachineTests.A_different_seed_can_produce_a_different_status_sequence`
builds a second machine on seed `7` instead and asserts the paths differ.
The remaining two tests substitute `IRandomSource` entirely with
NSubstitute rather than relying on any seed at all:
`ProbabilisticTransitMachineTests.A_draw_below_the_delay_probability_transitions_to_Delayed`
scripts `NextDouble()` to return `0.1` against a `delayProbability` of `0.5`
and asserts `Step(InTransit)` returns `Delayed`;
`ProbabilisticTransitMachineTests.A_draw_at_or_above_the_delay_probability_transitions_to_Cleared`
scripts `0.5` against the same threshold and asserts `Cleared` comes back,
pinning down the boundary as inclusive on the clearing side.

`SeededRandomSource` carries a local, documented suppression of CA5394, the
analyzer rule flagging `System.Random` as an insecure randomness source:

```csharp
public double NextDouble()
{
#pragma warning disable CA5394
    return _random.NextDouble();
#pragma warning restore CA5394
}
```

The suppression is scoped to this one method and justified in the comment
beside it: `ProbabilisticTransitMachine` drives a transit-delay simulation,
not a security decision, nothing here derives a token, a key, or a password
from the draw, and the entire reason the draw sits behind `IRandomSource`
in the first place is so a test can substitute it rather than depend on
`System.Random`'s own unpredictability. A cryptographically secure
generator would make the sequence unseedable and the test suite above
impossible to write deterministically, for no benefit, because nothing
downstream of this draw treats its output as a secret. That reasoning is
exactly what CA5394 asks a reviewer to confirm before a suppression goes
in, and it belongs next to the suppression itself rather than in a
code-review comment thread that will not travel with the file.

{% include excalidraw.html name="13-probabilistic-transit-model" caption="Figure 13.2: The stochastic exit from InTransit. ProbabilisticTransitMachine.Step draws IRandomSource.NextDouble() and compares it against delayProbability, sending the consignment to Delayed and back to InTransit to retry below the threshold, or on to Cleared and Delivered at or above it. SeededRandomSource wraps that draw in a seeded System.Random so two machines built on the same seed replay the identical sequence of statuses." %}

## Build, run, observe

The example lives in `examples/13-state-and-finite-state-machines` and is
standalone-testable from its own directory, with no container anywhere in
the loop:

```sh
cd examples/13-state-and-finite-state-machines
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. The suite splits across the three files this chapter
walks through: two `TransitionsTests` cases proving the transition table
itself, three `ConsignmentStateMachineTests` cases proving the context
object advances, rejects, and records correctly, and four
`ProbabilisticTransitMachineTests` cases proving the seeded source replays
and diverges correctly and the scripted substitute picks the right branch
on either side of the delay threshold. Observed locally on Fedora with the
.NET 10 SDK:

```
Test run summary: Passed!
  total: 9
  failed: 0
  succeeded: 9
  skipped: 0
  duration: 1s 479ms
```

## When not to use it

A finite state machine, in either form this chapter builds, is for a
lifecycle with more than one or two real states and transition rules strict
enough that an illegal move is a defect worth catching at the point it
happens. A consignment tracked only as "not yet delivered" versus
"delivered" has exactly one meaningful transition and no illegal moves to
guard against beyond setting the same flag twice, and that case needs
nothing more than a single boolean, not a `Transitions` table, a custom
exception type, and a `TrackingHistory`. Building the machinery this
chapter builds for a system that never actually enforces an ordering
between its states is ceremony with no transition rule behind it to
justify the extra types.

The second failure mode is modeling a lifecycle as a strict linear sequence
when the real process branches, loops, or allows states to be revisited in
ways the chosen state set cannot express. A consignment that can be
returned to the sender after delivery, re-routed mid-transit to a different
destination, or split into partial shipments that each carry their own
status needs a state set and transition table that account for those paths
explicitly; forcing that process through the four-event, five-state happy
path this chapter builds would mean rejecting real, legal operations as
`InvalidTransitionException` simply because the table that was written down
never anticipated them. The fix at that point is adding the missing states
and events to the table, not loosening `Transitions.Next` to accept
anything so the exception stops firing.

> **AI sidebar.** Asked to add a `Cancelled` status to this lifecycle, an
> AI pairing partner will sometimes generate a full `IConsignmentState`
> class hierarchy, one new class plus edits to five existing ones, for a
> change that `Transitions.Next` handles with two or three new switch arms
> and one new enum member, because class-per-state is the version of State
> most prevalent in older training material. A second, more consequential
> failure shows up inside the switch expression itself: a generated
> transition table that omits the `_ =>` catch-all, either leaving the
> compiler's CS8509 warning unaddressed or, worse, suppressing it with a
> pragma instead of adding the arm, so an illegal transition falls through
> to whatever the last matched arm happened to return instead of failing
> loudly. Check a generated transition table against two questions this
> chapter has already answered directly: does every arm correspond to a
> real, named (status, event) pair the domain actually allows, and does the
> catch-all arm throw rather than silently returning a plausible-looking
> status. A table that answers no to the second question is not a stricter
> state machine with one missing case; it is a flag-based system wearing a
> switch expression's syntax, with the exact silent-failure mode this
> chapter's state machine exists to rule out.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of the State pattern, including the class-per-state shape this chapter's switch expression replaces for a lifecycle with no per-state fields.
- Microsoft Learn, [Switch expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression): the authority for switch-expression exhaustiveness checking and the CS8509 warning a missing catch-all arm produces.
- Microsoft Learn, [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching): the authority for tuple patterns over multiple values, the shape `Transitions.Next` uses to match `(ConsignmentStatus, HandlingEventType)` pairs.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
