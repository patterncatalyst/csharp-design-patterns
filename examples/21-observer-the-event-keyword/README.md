# 21-observer-the-event-keyword

Observer expressed two ways over the same handling feed, both carrying the
same `HandlingEvent` record (`ConsignmentId`, `Status`, `Location`,
`OccurredAt`):

- **The `event` keyword.** `ConsignmentTracker` exposes a multicast
  `event EventHandler<HandlingReportedEventArgs>? HandlingReported`.
  `CustomerNotifier` and `AuditLog` each attach with `+=` and record what
  they are handed; `Report` appends to `History` and then raises the event
  through a null-conditional invocation
  (`HandlingReported?.Invoke(this, ...)`), so reporting with no subscribers
  attached never throws. Detaching a handler with `-=` stops delivery to it
  without disturbing any other attached handler.
- **Reaching past `event` to `IObservable<T>`.** `HandlingEventStream` wraps
  a System.Reactive `Subject<HandlingEvent>` behind an
  `IObservable<HandlingEvent> Events` property. `Publish` calls `OnNext`,
  `Complete` calls `OnCompleted`, and `RecordingObserver` implements
  `IObserver<HandlingEvent>` directly to capture what it receives. This is
  the shape to reach for once a subscriber needs operator composition,
  a uniform error channel, or an explicit completion signal, things a bare
  `event` does not give.

**What the tests prove.** `ConsignmentTrackerTests` reports two handling
events through a `ConsignmentTracker` with both `CustomerNotifier` and
`AuditLog` attached and asserts both receive each event in order; detaches
the notifier mid-sequence and asserts it stops receiving while the audit
log keeps receiving; asserts `Report` always appends to `History`
regardless of who is listening; and asserts reporting with zero subscribers
attached does not throw. `HandlingEventStreamTests` subscribes a
`RecordingObserver` to `HandlingEventStream.Events` and asserts it receives
published events in order; asserts `Complete` drives
`RecordingObserver.Completed` to `true` through `OnCompleted`; and
publishes one event before a second observer subscribes, showing the
stream is hot, the late subscriber never sees the earlier event, in
contrast to the event side's `ConsignmentTracker.History`, which keeps
every event a tracker has ever reported regardless of when a reader looks.

Shipping domain, no containers: handling events are pure records, and
`ConsignmentTracker` and `HandlingEventStream` are synchronous, in-process
publishers, no network or I/O to fake out.

## Build and test

```sh
cd examples/21-observer-the-event-keyword
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
