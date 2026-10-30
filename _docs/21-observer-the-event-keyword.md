---
title: "Observer: the event keyword"
order: 21
layout: tutorial
sectionid: docs
part: "coordinating-traversing"
description: "The clearest case of a pattern dissolving into a language feature, and when to reach past event to IObservable<T>."
---

<!-- EXAMPLE (shipping domain): Handling events raised as cargo moves. -->

## Learning objectives

By the end of this chapter you can:

- Explain why `ConsignmentTracker`'s `event EventHandler<HandlingReportedEventArgs>?
  HandlingReported` already is the GoF Observer pattern, with the tracker as
  the subject and `CustomerNotifier` and `AuditLog` as independent observers
  neither one knows exists.
- Raise an event through the null-conditional `?.Invoke` pattern and explain
  why it keeps `Report` safe to call when zero subscribers are attached.
- Subscribe and unsubscribe handlers with `+=` and `-=`, and trace the
  lifetime risk a forgotten `-=` leaves behind.
- Build a `HandlingEventStream` that wraps a System.Reactive
  `Subject<HandlingEvent>` behind `IObservable<HandlingEvent>`, and implement
  `IObserver<HandlingEvent>` directly in `RecordingObserver`.
- Decide, from what a subscriber actually needs, when a plain event is
  enough and when reaching past it to `IObservable<T>` buys something a
  multicast delegate cannot.

## Why this matters

A consignment moves through a fixed sequence of checkpoints: received at
origin, loaded onto a vessel or truck, in transit, sometimes held at
customs, cleared, delivered. Each checkpoint produces one scan, a status, a
location, and a timestamp, and several unrelated parts of a system want to
react the moment that scan happens. A customer-facing notifier wants to
send an update. A compliance system wants to append a permanent audit
record that can never be edited after the fact. A dashboard that does not
exist yet will eventually want to update a live map of every consignment
in motion. The component doing the actual scanning, something close to a
terminal gate or a customs desk, has exactly one job: record that the scan
happened and announce it. It should never need to import a notification
library, open a channel to an audit service, or know a dashboard is
planned, just to do that one job.

That is the shape the Observer pattern names: a subject that announces an
event, and any number of observers that react to it, with the subject
holding no reference to what reacting means for any one of them. A
consignment tracker should be able to gain a new kind of subscriber,
without changing a single line in the tracker itself, the same way a new
kind of reader can start listening to a radio broadcast without the
transmitter changing anything about how it broadcasts. Before any of the
mechanics below, this is the point worth making directly: C# gives this
relationship a dedicated keyword, `event`, and for the large share of
in-process cases a reader will meet, Observer and `event` are the same
idea wearing one name instead of the four or five types the original
pattern catalog describes.

{% include excalidraw.html name="21-observer-the-event-keyword" caption="Figure 21.1: ConsignmentTracker raises its event EventHandler<HandlingReportedEventArgs>? HandlingReported through a null-conditional Invoke, reaching CustomerNotifier and AuditLog, each subscribed with += and detachable with -=; HandlingEventStream wraps a System.Reactive Subject<HandlingEvent> behind IObservable<HandlingEvent>, and a RecordingObserver implementing IObserver<HandlingEvent> subscribes to it directly." %}

## The event keyword: Observer built directly into the language

The example behind this chapter lives in
`examples/21-observer-the-event-keyword`. Every handling scan in it is the
same record, carrying a consignment identifier, where the lifecycle stands,
where the scan happened, and when:

```csharp
public enum ConsignmentStatus
{
    Received,
    Loaded,
    InTransit,
    CustomsHold,
    Cleared,
    Delivered,
}

public sealed record HandlingEvent(
    string ConsignmentId,
    ConsignmentStatus Status,
    string Location,
    DateTimeOffset OccurredAt);
```

`ConsignmentTracker` is the subject. It does not hold a list of observers
it manages by hand, does not expose a `Subscribe`/`Unsubscribe` pair of
methods, and does not define an `IObserver`-style interface a listener has
to implement. It declares one `event` field and raises it:

```csharp
public sealed class ConsignmentTracker
{
    private readonly List<HandlingEvent> _history = [];

    public event EventHandler<HandlingReportedEventArgs>? HandlingReported;

    public IReadOnlyList<HandlingEvent> History => _history;

    public void Report(HandlingEvent handling)
    {
        _history.Add(handling);
        HandlingReported?.Invoke(this, new HandlingReportedEventArgs(handling));
    }
}
```

Three details here carry the whole chapter, and each one is a direct answer
to a design question the GoF pattern leaves open and the CLR closes for
good. First, the event's type is the generic `EventHandler<TEventArgs>`
delegate, `EventHandler<HandlingReportedEventArgs>`, rather than a bespoke
delegate type declared just for this tracker. The base class library
standardized on exactly this generic form so every .NET event, regardless
of what payload it carries, has the identical two-parameter shape, a
sender and a typed argument, and a custom delegate type exists only when a
signature cannot fit that shape at all. Second, the payload itself is
`HandlingReportedEventArgs`, a sealed class deriving from `EventArgs` that
wraps the `HandlingEvent` record rather than handing a bare `HandlingEvent`
to subscribers directly:

```csharp
public sealed class HandlingReportedEventArgs(HandlingEvent handling) : EventArgs
{
    public HandlingEvent Handling { get; } = handling;
}
```

The convention this follows, an `EventArgs` subclass per distinct event
payload, exists so a later version of `ConsignmentTracker` can add a second
property to the argument class, a carrier identifier, a confidence score on
an automated scan, without breaking every existing handler's signature. A
handler written against `HandlingReportedEventArgs e` still compiles and
still works once `e` grows a new property neither it nor its author knew
about when it was written; a handler written against a bare `HandlingEvent`
parameter would have nowhere to add that property without changing the
signature every subscriber already implements.

Third, and this is the detail a reader new to events most often gets wrong,
`Report` raises the event with `HandlingReported?.Invoke(...)`, a
null-conditional call on the event field itself. An event with no
subscribers attached is a null delegate field, exactly like any other
reference-typed field nobody has assigned, and calling `Invoke` on a null
reference throws a `NullReferenceException`. The null-conditional operator
short-circuits that call entirely when `HandlingReported` is null, so
`Report` can run, append to `_history`, and return successfully whether
zero, one, or a dozen handlers are listening, with no `if (HandlingReported
!= null)` guard anywhere in the method and no race between a null check and
an invocation in a multithreaded caller. `ConsignmentTrackerTests.Report_with_no_subscribers_does_not_throw`
constructs a bare `ConsignmentTracker`, calls `Report` on it with nothing
ever subscribed, and asserts with `Record.Exception` that the call throws
nothing while the event still lands in `History`. `ConsignmentTrackerTests.Report_records_the_event_in_history`
checks the other half of the same method directly: `Report` always appends
to `_history` first, so a tracker's own record of what happened to a
consignment never depends on whether anyone was listening at the time it
happened.

One more naming choice in `ConsignmentTracker` runs backward from what a
reader might expect at first. The method that raises `HandlingReported` is
named `Report`, not `RaiseHandlingReported` or `OnHandlingReported`. .NET's
own design guidance for events, enforced here by the CA1030 analyzer rule,
reserves names starting with `On` or `Raise` for the protected virtual
method a base class exposes specifically so a derived class can override
the moment an event fires; a public method that does domain work and, as
part of that work, raises an event keeps a name that describes the domain
action, `Report`, not the event-raising mechanism
underneath it. `CustomerNotifier` and `AuditLog` are the two observers in
this example, and neither one has the slightest idea the other exists:

```csharp
public sealed class CustomerNotifier
{
    private readonly List<HandlingEvent> _notifications = [];

    public IReadOnlyList<HandlingEvent> Notifications => _notifications;

    public void OnHandlingReported(object? sender, HandlingReportedEventArgs e)
    {
        _notifications.Add(e.Handling);
    }
}
```

`AuditLog` has the identical shape, a list it appends to and an
`OnHandlingReported(object?, HandlingReportedEventArgs)` method matching
the `EventHandler<HandlingReportedEventArgs>` signature exactly, so either
one can subscribe with a plain `+=`:

```csharp
var tracker = new ConsignmentTracker();
var notifier = new CustomerNotifier();
var auditLog = new AuditLog();
tracker.HandlingReported += notifier.OnHandlingReported;
tracker.HandlingReported += auditLog.OnHandlingReported;
```

An `event` field is a multicast delegate under a restricted interface:
outside the class that declares it, the only operations a caller can
perform are `+=` and `-=`; direct assignment, invocation, or reading the
current invocation list are all compiler errors from outside
`ConsignmentTracker`. That restriction is exactly what makes a tracker
safe to hand out widely. Any number of subscribers can attach without
coordinating with each other or with the tracker's author, and
`ConsignmentTrackerTests.Both_subscribers_receive_each_reported_event_in_order`
proves the multicast delivery directly: it attaches both `notifier` and
`auditLog`, reports two handling events in sequence, and asserts both
`notifier.Notifications` and `auditLog.Entries` end up holding the
identical two events in the identical order, confirming one `Report` call
fans out to every attached handler, not just the first one wired up.
Detaching one handler with `-=` while leaving the others in place is just
as direct an operation as attaching one, and
`ConsignmentTrackerTests.Unsubscribing_one_handler_stops_delivery_to_it_while_the_other_still_receives`
reports one event with both handlers attached, detaches `notifier` with
`tracker.HandlingReported -= notifier.OnHandlingReported`, reports a second
event, and asserts `notifier.Notifications` stopped after the first event
while `auditLog.Entries` kept growing, exactly the independence the pattern
promises: removing one observer changes nothing about how any other
observer is served.

That same independence is what makes a forgotten `-=` worth tracking as a
lifetime concern rather than dismissing it as a cleanup detail. Attaching a
handler with `+=` adds a delegate instance to the tracker's own invocation
list, and that delegate instance carries a reference to the subscriber
object the method belongs to, `notifier` or `auditLog` in this example.
As long as `tracker.HandlingReported` still lists that delegate, the
subscriber stays reachable through the tracker, whether or not anything
else in the program still needs it. A `ConsignmentTracker` that lives for
the life of the application, registered once in a composition root and
never replaced, will keep every subscriber that ever attached to it
reachable for that same lifetime unless something explicitly calls `-=`.
This is the classic .NET event leak, and the fix is the same operation the
test above already demonstrates: a subscriber that is done listening calls
`-=` on its way out, the same way it called `+=` on its way in, so the
tracker's invocation list only ever holds references to subscribers that
still want to hear from it.

## When to reach past event to IObservable<T>

A plain `event` answers every question this chapter has asked so far: fan
out to any number of independent subscribers, detach one without
disturbing the rest, raise safely with nobody listening. It does not
answer a different set of questions a subscriber sometimes needs answered:
how does a subscriber combine this stream of handling events with a second
stream, filter it down to only the events that matter to it, or find out
definitively that no more events are coming rather than simply never
hearing from the publisher again. `IObservable<T>` and its partner
`IObserver<T>`, the Reactive Extensions shape distributed through the
`System.Reactive` package, pinned at version 7.0.0 for this example, answer
exactly those questions, and `HandlingEventStream` is this example's
version of the same handling feed built on that shape instead of on
`event`:

```csharp
public sealed class HandlingEventStream : IDisposable
{
    private readonly Subject<HandlingEvent> _subject = new();

    public IObservable<HandlingEvent> Events => _subject;

    public void Publish(HandlingEvent handling)
    {
        _subject.OnNext(handling);
    }

    public void Complete()
    {
        _subject.OnCompleted();
    }

    public void Dispose()
    {
        _subject.Dispose();
    }
}
```

`Subject<HandlingEvent>` is the one piece System.Reactive contributes that
the base class library has no equivalent for: a single object that is
simultaneously an `IObserver<HandlingEvent>`, something a publisher can
push values into, and an `IObservable<HandlingEvent>`, something a
consumer can subscribe to. `HandlingEventStream` keeps that `Subject`
private and exposes only the `IObservable<HandlingEvent>` half through
`Events`, so a consumer of `HandlingEventStream` can subscribe but can
never call `OnNext` directly and inject an event the stream's own
`Publish` method did not originate. `Publish` forwards to the subject's
`OnNext`, and `Complete` forwards to `OnCompleted`, the signal that marks
the sequence finished and tells every current and future subscriber no
further event will ever arrive through this stream again. `RecordingObserver`
is the plain `IObserver<HandlingEvent>` implementation this example
subscribes to that stream:

```csharp
public sealed class RecordingObserver : IObserver<HandlingEvent>
{
    private readonly List<HandlingEvent> _received = [];

    public IReadOnlyList<HandlingEvent> Received => _received;

    public bool Completed { get; private set; }

    public Exception? Error { get; private set; }

    public void OnNext(HandlingEvent value)
    {
        _received.Add(value);
    }

    public void OnCompleted()
    {
        Completed = true;
    }

    public void OnError(Exception error)
    {
        Error = error;
    }
}
```

`IObserver<T>` fixes exactly three methods a subscriber implements, and
that fixed shape is itself one of the things `IObservable<T>` buys over a
plain event: every subscriber to any observable sequence, regardless of
what it carries, answers the identical three questions, here is the next
value, the sequence is finished, or the sequence ended in an error, through
the same three-method contract. A plain `event` has no equivalent to
`OnError`; an exception thrown inside a handler propagates straight back
through the publisher's own `Invoke` call, and there is no uniform channel
for a publisher to tell every subscriber an error terminated the sequence
the way `OnError` does here. `HandlingEventStreamTests.Subscribed_observer_receives_published_events_in_order`
subscribes a `RecordingObserver` to `stream.Events`, publishes two handling
events in sequence, and asserts `observer.Received` holds both in the
order they were published, the `IObservable<T>` equivalent of the
multicast delivery the event-based tests already proved.
`HandlingEventStreamTests.Complete_signals_the_observer_as_completed`
calls `stream.Complete()` with an observer already subscribed and asserts
`observer.Completed` is true and `observer.Error` is null afterward,
exercising the explicit completion signal a bare `event` has no vocabulary
for at all.

The behavior that most sharply separates the two shapes is what a late
subscriber sees. `ConsignmentTracker.History` is a durable list that grows
with every `Report` call and stays available to any reader that asks for
it afterward, regardless of whether that reader was subscribed at the time
a given event was reported. `Subject<HandlingEvent>` keeps no such
replay buffer; it is a hot sequence, one that only delivers a value to
whichever observers happen to be subscribed at the exact moment `OnNext`
runs, and a subscriber that attaches after a value has already gone
through never sees that value, no matter how recently it was published.
`HandlingEventStreamTests.Subscriber_that_subscribes_after_an_earlier_publish_misses_that_event`
publishes one handling event with nobody subscribed yet, subscribes a
`RecordingObserver` afterward, publishes a second event, and asserts the
late observer's `Received` list contains only the second event while
`Assert.DoesNotContain` confirms the first never arrives. A reader
reaching for `IObservable<T>` because of its composition operators or its
`OnError` channel needs to plan around this directly: if a late subscriber
needs to see history, the subscriber has to ask something other than the
`Subject` itself, `ConsignmentTracker.History` in this example, because
the observable side of this pairing was never built to remember what it
already sent.

{% include excalidraw.html name="21-event-and-iobservable" caption="Figure 21.2: HandlingEventStream wraps a System.Reactive Subject<HandlingEvent> and exposes only IObservable<HandlingEvent> through Events; Publish forwards to OnNext and Complete forwards to OnCompleted, reaching a RecordingObserver subscribed to it, and a late subscriber misses whatever the hot Subject already pushed, unlike ConsignmentTracker's retained History." %}

`HandlingEventStream` carries one naming detail in a single sentence: a
Roslyn analyzer flags a type named `*Stream` as implying an implementation
of `System.IO.Stream`, which this class is not, and the
example carries a scoped `SuppressMessage` on that rule because the name
describes the Observer-over-`IObservable<T>` role this chapter is built
around, not a byte stream. It is a naming footnote next to the behavior
the type actually demonstrates, not the reason to reach for `IObservable<T>`
in the first place.

## Build, run, observe

Both shapes in this example are synchronous and in-process, so the suite
needs nothing beyond the local .NET 10 SDK. The example is
standalone-testable from its own directory:

```sh
cd examples/21-observer-the-event-keyword
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 7
  failed: 0
  succeeded: 7
  skipped: 0
  duration: 1s 625ms
```

The seven cases split evenly across the two shapes. `ConsignmentTrackerTests`
proves the `event` half: both `CustomerNotifier` and `AuditLog` receive
every reported handling event in order, detaching one handler stops
delivery to it while the other keeps receiving, `Report` always appends to
`History` regardless of who is listening, and reporting with zero
subscribers attached never throws. `HandlingEventStreamTests` proves the
`IObservable<T>` half: a subscribed `RecordingObserver` receives published
events in order, `Complete` drives `OnCompleted` and leaves `OnError`
untouched, and a subscriber that attaches after an earlier publish misses
that earlier event entirely, the hot-stream behavior that has no
counterpart on the event side.

## When not to use it

An `IObservable<T>` stream, built on `Subject<T>` or any other System.Reactive
source, costs a package reference, a `Subscribe`/`IDisposable` lifecycle a
caller has to manage, and three methods, `OnNext`, `OnCompleted`, and
`OnError`, an observer has to implement where a plain delegate target would
have needed one. That cost pays for something specific: operator
composition across more than one stream, a uniform channel for signaling an
error to every subscriber at once, or an explicit, provable end to a
sequence. A single publisher with a single kind of subscriber, no filtering,
no merging two feeds together, and no need to distinguish "nothing has
happened yet" from "nothing will ever happen again," gets none of that back
for the extra ceremony. `ConsignmentTracker`'s own `HandlingReported` event
is the right default for exactly that reason: `CustomerNotifier` and
`AuditLog` each want every handling event as it happens, nothing more
elaborate, and a `Subject<HandlingEvent>` standing in for that would add an
`IDisposable` subscription and a three-method observer to a relationship
`+=` and `-=` already express completely.

An `event` itself is more than a single fixed relationship needs. A
`ConsignmentTracker` built only to report its own scans directly to one
collaborator that will only ever be that one collaborator, with no second
subscriber planned or conceivable, gets nothing from declaring
`HandlingReported` as a multicast event instead of simply calling a method
on that one collaborator directly from inside `Report`. The event adds an
`EventArgs` subclass, a public field only `+=` and `-=` can touch, and a
null-conditional invocation to guard against a case, zero subscribers, that
cannot happen when exactly one subscriber is baked in from the start. The
tell for both of these misreadings is the same: count how many kinds of
subscriber a publisher actually serves, and whether that count could ever
grow. Zero variation and one fixed collaborator wants a direct call.
Multiple independent subscribers that come and go wants `event`. Streams
that need to be filtered, combined, or definitively ended want
`IObservable<T>`.

> **AI sidebar.** Asked to make a status change "reactive" or "observable,"
> an AI pairing partner will sometimes reach straight for a `Subject<T>`
> and an `IObservable<T>` property, package reference and all, for a
> publisher that has exactly one kind of subscriber and never needs an
> `OnError` channel or an operator chain, where a one-line
> `event EventHandler<T>?` would have said the same thing with nothing to
> install and nothing to dispose. Check a generated answer against the
> question this chapter keeps coming back to: does a subscriber need to
> compose this stream with another one, react uniformly to an error across
> every listener, or detect a definitive end to the sequence. A publisher
> that just needs to tell zero or more listeners something happened, in
> order, as it happens, gets everything from a plain `event` that the
> generated `Subject<T>` would have given it, at a fraction of the
> ceremony.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Observer, a subject maintaining a list of dependents and notifying them of a state change, which `ConsignmentTracker` implements through the CLR's own `event` keyword rather than a hand-rolled subscriber list.
- Microsoft Learn, [Handle and raise events](https://learn.microsoft.com/en-us/dotnet/standard/events/) and [Events](https://learn.microsoft.com/en-us/dotnet/csharp/events-overview): the authority for the `EventHandler<TEventArgs>` convention, the `EventArgs` subclass pattern, and the null-conditional raise idiom `ConsignmentTracker.Report` relies on.
- Microsoft Learn, [`IObservable<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.iobservable-1) and [`IObserver<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.iobserver-1): the authority for the three-method observer contract `RecordingObserver` implements and the push-based subscription model `HandlingEventStream.Events` exposes.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
