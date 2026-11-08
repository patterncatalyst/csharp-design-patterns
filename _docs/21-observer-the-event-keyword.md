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

- Recognize `ConsignmentTracker`'s `event EventHandler<HandlingReportedEventArgs>? HandlingReported` as the GoF Observer, with two observers that don't know each other exists.
- Raise an event with the null-conditional `?.Invoke` so `Report` is safe with zero subscribers.
- Subscribe and unsubscribe with `+=` and `-=`, and explain the lifetime risk of a forgotten `-=`.
- Wrap a System.Reactive `Subject<HandlingEvent>` behind `IObservable<HandlingEvent>` and implement `IObserver<HandlingEvent>` directly.
- Decide from what a subscriber needs whether a plain event is enough or `IObservable<T>` is worth its cost.

## Why this matters

A consignment passes fixed checkpoints: received at origin, loaded, in transit, sometimes held at customs, cleared, delivered. Each checkpoint produces one scan, and several unrelated parts of a system react to it. A customer notifier sends an update, a compliance system appends an audit record that's never edited, and a live dashboard will want it later. The component doing the scan, close to a terminal gate or a customs desk, should record the scan and announce it, without importing a notification library, opening a channel to an audit service, or knowing a dashboard is planned.

Observer names that relationship: a subject announces, any number of observers react, and the subject holds no idea of what reacting means for any of them. C# gives it a keyword, `event`. For most in-process cases, Observer and `event` are the same idea under one name instead of the several types the original catalog describes.

{% include excalidraw.html name="21-observer-the-event-keyword" caption="Figure 21.1: ConsignmentTracker raises an event that reaches two independent observers, each attached with += and detachable with -=. A separate reactive stream wraps the same kind of event behind an observable interface instead." %}

## Pattern: Observer

### The problem

A tracker must notify a customer notifier and an audit log of every scan, in order, and gain a third subscriber later without changing the tracker. Each scan is one record:

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

### The solution

An **Observer** defines a one-to-many dependency so that when a subject changes, every dependent is notified without the subject knowing what they are.

### How C# models it

The subject declares one `event` and raises it. There's no hand-managed observer list, no `Subscribe`/`Unsubscribe` pair and no observer interface:

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

The event uses the generic `EventHandler<TEventArgs>` delegate rather than a bespoke delegate type. The base class library standardized on this shape, a sender and a typed argument, for every event; a custom delegate is for a signature that can't fit it.

### The EventArgs payload

The payload is an `EventArgs` subclass wrapping the record, not the bare record:

```csharp
public sealed class HandlingReportedEventArgs(HandlingEvent handling) : EventArgs
{
    public HandlingEvent Handling { get; } = handling;
}
```

A later version can add a carrier identifier or a scan confidence score to the argument class without changing any handler's signature. A handler that takes a bare `HandlingEvent` has nowhere to put the new property.

### Raising safely

An event with no subscribers is a null delegate field, and calling `Invoke` on null throws `NullReferenceException`. `HandlingReported?.Invoke(...)` skips the call when the field is null, so `Report` works with zero, one or a dozen handlers, with no `if (HandlingReported != null)` guard and no race between a check and the call. `Report` appends to `_history` first, so the tracker's record never depends on whether anyone was listening.

The raising method is named `Report`, not `RaiseHandlingReported` or `OnHandlingReported`. .NET's event design guidance, enforced by analyzer rule CA1030, reserves `On` and `Raise` names for the protected virtual method a base class exposes so a derived class can override the raise. A public method that does domain work keeps a domain name.

### Subscribing and unsubscribing

The two observers share a shape, a list they append to and a method matching `EventHandler<HandlingReportedEventArgs>`:

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

`AuditLog` is the same with an `Entries` list. Either subscribes with `+=`:

```csharp
var tracker = new ConsignmentTracker();
var notifier = new CustomerNotifier();
var auditLog = new AuditLog();
tracker.HandlingReported += notifier.OnHandlingReported;
tracker.HandlingReported += auditLog.OnHandlingReported;
```

An `event` is a multicast delegate behind a restricted interface. Outside the declaring class, only `+=` and `-=` compile; assignment, invocation and reading the invocation list are errors. That makes a tracker safe to hand out widely.

| Action | Result |
| --- | --- |
| Both attached, two events reported | Both observers hold the same two events in the same order |
| `tracker.HandlingReported -= notifier.OnHandlingReported`, then a second event | The notifier stops at the first event; the audit log keeps growing |
| Nothing attached, `Report` called | No exception; the event lands in `History` |

### The forgotten -=

`+=` adds a delegate to the tracker's invocation list, and the delegate holds a reference to the subscriber. While the list holds it, the subscriber stays reachable through the tracker whether or not anything else needs it. A tracker registered once in a composition root keeps every subscriber that ever attached alive for the application's lifetime. That's the classic .NET event leak. A subscriber that's done listening calls `-=` on its way out, as it called `+=` on its way in.

### Reaching past event to IObservable&lt;T&gt;

A plain event fans out, detaches cleanly and raises safely with nobody listening. It can't combine a feed with a second stream, filter it with operators, report an error to every listener, or say that no more events are coming. `IObservable<T>` and `IObserver<T>`, with the Reactive Extensions operators from the `System.Reactive` package, can:

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

`Subject<HandlingEvent>` is both an `IObserver<HandlingEvent>`, which a publisher pushes into, and an `IObservable<HandlingEvent>`, which a consumer subscribes to. The stream keeps the subject private and exposes only the observable half, so a consumer can't call `OnNext` and inject an event. `Complete` forwards to `OnCompleted`, which tells every subscriber nothing more will arrive.

A subscriber implements three methods:

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

Every observer of every sequence answers the same three signals: next value, finished, failed. An event has no `OnError`: an exception in a handler propagates back through the publisher's `Invoke`, and there's no uniform way to tell every subscriber that an error ended the sequence. A subscribed `RecordingObserver` receives published events in order; after `Complete`, `Completed` is true and `Error` is null.

| Need | `event` | `IObservable<T>` |
| --- | --- | --- |
| Fan out to many subscribers | Yes | Yes |
| Filter, merge or combine streams | No | Operators |
| Signal an error to every subscriber | No | `OnError` |
| Signal the end of the sequence | No | `OnCompleted` |
| Cost | None beyond the field | A package, three methods, an `IDisposable` subscription |

### Hot subjects and late subscribers

`ConsignmentTracker.History` is a durable list any reader can ask for later. `Subject<T>` keeps no replay buffer. It's a hot sequence: it delivers a value only to observers subscribed when `OnNext` runs. Publish one event with nobody subscribed, subscribe, publish a second, and the late observer sees only the second. If a late subscriber needs history, it asks something that remembers, `ConsignmentTracker.History` here, not the subject.

{% include excalidraw.html name="21-event-and-iobservable" caption="Figure 21.2: A reactive stream wraps a Subject behind an observable interface; publishing forwards to the subject and completing signals every subscriber. A late subscriber misses events published before it attached, unlike the tracker's retained history." %}

A Roslyn analyzer flags a type named `*Stream` as implying a `System.IO.Stream`, which `HandlingEventStream` isn't, so a scoped `SuppressMessage` keeps the name.

## When not to use it

- **`IObservable<T>`.** Skip it for one publisher with one kind of subscriber, no filtering, no merging and no need to tell "nothing yet" from "nothing ever." `CustomerNotifier` and `AuditLog` want every event as it happens; a `Subject<HandlingEvent>` would add an `IDisposable` subscription and a three-method observer to what `+=` and `-=` already express.
- **`event`.** Skip it when the tracker reports to one fixed collaborator and no second subscriber is conceivable. Call the collaborator from `Report`. The event adds an `EventArgs` subclass, a field only `+=` and `-=` can touch, and a null guard for a case that can't happen.

| Subscribers | Use |
| --- | --- |
| One fixed collaborator | A direct call |
| Several independent ones that come and go | `event` |
| Streams to filter, combine or end definitively | `IObservable<T>` |

<blockquote class="ai-sidebar" markdown="1">
**Specifying an observer.** "Make the tracker observable" doesn't say whether subscribers need operators, an error channel or an end signal, or how they unsubscribe. State those in the prompt:

> In `ConsignmentTracker`, declare `public event EventHandler<HandlingReportedEventArgs>? HandlingReported` and raise it from `Report` with `HandlingReported?.Invoke` after adding the event to `History`, so a tracker with no subscribers doesn't throw. Give `CustomerNotifier` and `AuditLog` an `OnHandlingReported(object? sender, HandlingReportedEventArgs e)` handler and unsubscribe with `-=`. Don't add `System.Reactive` or a `Subject<T>` to the tracker; that belongs in `HandlingEventStream`, where a subscriber needs `OnCompleted`.

Run `dotnet test --project ConsignmentTracking.Tests --filter ConsignmentTrackerTests`, and confirm `ConsignmentTracker.cs` never mentions `IObservable`.
</blockquote>

## Build, run, observe

```sh
cd examples/21-observer-the-event-keyword
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. Both shapes are synchronous and in-process. All 7 tests pass: four for the event (multicast order, `-=`, history, zero subscribers) and three for the stream (ordered delivery, completion, the late subscriber missing an earlier event).

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Observer, a subject maintaining a list of dependents and notifying them of a state change.
- Microsoft Learn, [Handle and raise events](https://learn.microsoft.com/en-us/dotnet/standard/events/) and [Events](https://learn.microsoft.com/en-us/dotnet/csharp/events-overview): the `EventHandler<TEventArgs>` convention, the `EventArgs` subclass pattern and the null-conditional raise.
- Microsoft Learn, [`IObservable<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.iobservable-1) and [`IObserver<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.iobserver-1): the three-method observer contract and the push-based subscription model.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
