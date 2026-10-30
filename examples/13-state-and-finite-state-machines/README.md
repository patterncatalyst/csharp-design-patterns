# 13-state-and-finite-state-machines

The State pattern expressed as a finite state machine over a consignment's
lifecycle, plus a second machine where one transition is probabilistic
instead of event-driven.

- **The transition table.** `ConsignmentStatus` is the state set (`Booked`,
  `Loaded`, `InTransit`, `Cleared`, `Delivered`, `Delayed`); `HandlingEventType`
  is the event set a carrier reports (`Load`, `Depart`, `ClearCustoms`,
  `Deliver`). `Transitions.Next` is the whole pattern in one place: a switch
  expression over the `(current, event)` tuple, with every legal move as one
  arm and a catch-all that throws `InvalidTransitionException` for anything
  else. There is no `IState` hierarchy with one class per state; the table
  form keeps all four legal moves visible together.
- **The context object.** `ConsignmentStateMachine` holds the current
  `ConsignmentStatus`, starting at `Booked`, and a `TrackingHistory`.
  `Apply(HandlingEvent)` asks `Transitions.Next` for the resulting status,
  updates `CurrentStatus`, and appends a `HandlingRecord` pairing the event
  with that result, so the machine never branches on status itself.
- **A probabilistic variant.** `ProbabilisticTransitMachine` drives the same
  `ConsignmentStatus` states, but the exit from `InTransit` is a coin flip
  instead of a `ClearCustoms` event: a draw from `IRandomSource` below the
  configured delay probability goes to `Delayed`, otherwise the leg clears.
  `IRandomSource` abstracts the draw so `SeededRandomSource` (a thin wrapper
  over a seeded `System.Random`) makes the sequence reproducible, and a test
  can substitute a scripted draw without touching `System.Random` at all.

**What the tests prove.** `TransitionsTests` calls `Transitions.Next` directly:
`Booked` plus `Load` advances to `Loaded`; `Booked` plus `Deliver` throws
`InvalidTransitionException` because delivering before loading has no legal
transition. `ConsignmentStateMachineTests` drives the same cases through
`ConsignmentStateMachine.Apply`, then runs the full lifecycle, `Load`,
`Depart`, `ClearCustoms`, `Deliver`, and asserts `CurrentStatus` ends at
`Delivered` while `TrackingHistory` records all four steps with the right
resulting status at each one. `ProbabilisticTransitMachineTests` builds two
machines on `SeededRandomSource`s with the same seed and asserts
`Simulate` produces an identical status sequence, then shows a different seed
can diverge; it also substitutes `IRandomSource` with NSubstitute to prove a
draw below the delay probability lands on `Delayed` and a draw at or above it
lands on `Cleared`.

Shipping domain, no containers: every type here is a pure, synchronous
computation over in-memory state, no network or I/O to fake out.

## Build and test

```sh
cd examples/13-state-and-finite-state-machines
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
