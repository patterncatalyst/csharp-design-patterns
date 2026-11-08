---
title: "Dependency-breaking techniques for C# 14"
order: 42
layout: tutorial
sectionid: docs
part: "changing-inherited-code"
description: "InternalsVisibleTo, interface extraction, DI as a seam, and virtual as a deliberate act."
---

<!-- EXAMPLE (shipping domain): Breaking the same engine's dependencies. -->

## Learning objectives

By the end of this chapter you can:

- Name the five dependencies `Legacy.RateEngine.Quote` bakes in at the point of use: a newed-up `LegacyAuditLog`, the static `LegacyTariffBook.RateFor`, `Guid.NewGuid()`, `DateTime.UtcNow` and `Environment.UserName`.
- Explain why a `sealed` class with a non-virtual `Quote` closes every subclass seam, and prove that shape with reflection.
- Apply Extract Interface to the tariff lookup and the audit log, and Parameterize Constructor to take all four collaborators in one call.
- Choose between Parameterize Constructor and Parameterize Method by whether a value holds for the engine's lifetime or for one call.
- Open two seams that need no new interface, Extract and Override Call on `NewReference` and `InternalsVisibleTo` on `LaneValidation.IsKnownLane`, and verify the audit interaction the legacy form hid.

## Why this matters

Michael Feathers defines legacy code as code without tests: code nobody can change with the confidence a passing suite gives. By that definition the legacy `RateEngine` is legacy the day it ships. Everything it needs, a lane's rate, an audit sink, a reference, a timestamp and the caller's identity, arrives through a call inside the one method it exposes. Nothing can be overridden, substituted or observed from outside.

A test needs a **seam**: a point where it can change the program's behavior without editing the place where that behavior runs. In C#, `sealed` and non-virtual methods decide which seams are possible before any test exists. That isn't a defect to work around with a mocking framework that fakes anything. It's a job for named techniques: Extract Interface, Parameterize Constructor, Parameterize Method, Extract and Override Call, `InternalsVisibleTo`, and wrapping an ambient call behind an injected abstraction. None changes what the engine computes. Each changes how much of that computation a test can control and observe.

{% include excalidraw.html name="42-dependency-breaking-techniques" caption="Figure 42.1: The same rate-quoting engine shown twice: dependencies baked in at the call site on one side, broken out into constructor-injected and overridable seams on the other." %}

## A rate engine with five dependencies baked in

Every line of `Legacy.RateEngine.Quote` reaches outside the method for something a test can't control:

```csharp
public RateQuote Quote(RateRequest request)
{
    var audit = new LegacyAuditLog();
    var rate = LegacyTariffBook.RateFor(request.LaneCode);
    var reference = Guid.NewGuid().ToString("N");
    var quotedAt = DateTime.UtcNow;
    var quotedBy = Environment.UserName;

    var amount = rate * request.WeightKilograms;
    audit.Record($"Quoted {reference} for consignment {request.ConsignmentId}: {amount:0.00}");

    return new RateQuote(reference, request.ConsignmentId, amount, quotedBy, quotedAt);
}
```

| Dependency | Why a test can't reach it |
| --- | --- |
| `new LegacyAuditLog()` | Built inside the method and never leaves its scope; `LegacyAuditLog` also offers no way to read its entries back |
| `LegacyTariffBook.RateFor` | A `switch` in a static method; changing the rate means editing `LegacyTariffBook` |
| `Guid.NewGuid()` | Fresh every call; a test can check only length and character set |
| `DateTime.UtcNow` | Fresh every call; a test can check only a tolerance window around "now" |
| `Environment.UserName` | The process account, a fact about the environment rather than the quote |

None can be swapped from a subclass: `Legacy.RateEngine` is `sealed`, and `Quote` isn't `virtual` either. Reflection proves the shape without asserting on any unpredictable value. The type is sealed, offers only a public parameterless constructor, and `Quote` reports `IsVirtual` as `false`. `LegacyTariffBook`, a static class, compiles to a CLR type that is both `IsAbstract` and `IsSealed`, so there's no instance to substitute at all. Feathers' catalogue of dependency-breaking techniques exists for a type like this: production behavior worth keeping and no seam worth using.

{% include excalidraw.html name="42-feathers-catalogue-in-c-sharp" caption="Figure 42.2: Six classic dependency-breaking techniques from Michael Feathers reshape the same rate engine, replacing inline construction and ambient calls with injected interfaces and a narrow internal seam." %}

## Extract Interface: naming the tariff lookup and the audit log

Name an interface for exactly the members the engine calls, and give the engine that type instead of the concrete class:

```csharp
public interface ITariffBook
{
    decimal RateFor(string laneCode);
}
```

`StandardTariffBook` returns the same per-lane rates as `LegacyTariffBook.RateFor`, so the refactored engine quotes identically. The audit sink gets the same treatment:

```csharp
public interface IAuditLog
{
    void Record(string message);
}
```

`ListAuditLog`, the production implementation, exposes an `Entries` property, so any holder of the instance can read back what was recorded. Extract Interface alone buys a smaller surface to depend on. Pairing it with an inspectable implementation lets a test see what the engine told the audit log.

## Parameterize Constructor: four collaborators in one call

The interfaces do nothing until the engine stops constructing its collaborators. The refactored `RateEngine` takes four of them through one primary constructor; the caller's identity goes on a different seam:

```csharp
public class RateEngine(ITariffBook tariffs, IAuditLog audit, IClock clock, IReferenceGenerator references)
{
    public RateQuote Quote(RateRequest request, string quotedBy)
    {
        if (!LaneValidation.IsKnownLane(request.LaneCode))
        {
            throw new UnknownLaneException($"Unknown lane code '{request.LaneCode}'.");
        }

        var rate = tariffs.RateFor(request.LaneCode);
        var amount = rate * request.WeightKilograms;
        var reference = NewReference();
        var at = clock.UtcNow;

        audit.Record($"Quoted {reference} for consignment {request.ConsignmentId}: {amount:0.00}");

        return new RateQuote(reference, request.ConsignmentId, amount, quotedBy, at);
    }

    protected virtual string NewReference() => references.NewReference();
}
```

Whoever builds the engine now decides what backs each parameter:

| Parameter | Production | Test |
| --- | --- | --- |
| `tariffs` | `StandardTariffBook` | NSubstitute fake |
| `audit` | `ListAuditLog` | NSubstitute fake |
| `clock` | `SystemClock` | NSubstitute fake |
| `references` | `GuidReferenceGenerator` | NSubstitute fake |

The engine only calls the interface members it declared. It's also not `sealed`, which keeps the Extract and Override Call seam below available.

## Parameterize Method: a caller identity that belongs to one call

`quotedBy` is a method parameter, not a fifth constructor argument. The tariff book, audit sink, clock and reference generator are fixed for the life of one engine and back every quote it produces. The caller isn't: two operators can share one long-lived engine, each quoting for whoever is calling now. In the constructor, `quotedBy` would force a fresh engine per caller; as a settable property, a second call could overwrite it while the first was still running. Parameterize Method puts the value where it varies, replaces the ambient `Environment.UserName`, and the quote's `QuotedBy` is exactly the string the caller passed.

## Extract and Override Call: a seam that needs no interface

`NewReference` is a second seam onto the reference concern. Extract and Override Call pulls one call, `references.NewReference()`, into its own `protected virtual` method, so a subclass can replace it without touching the constructor or the rest of `Quote`:

```csharp
public sealed class FixedReferenceEngine(ITariffBook tariffs, IAuditLog audit, IClock clock, IReferenceGenerator references)
    : RateEngine(tariffs, audit, clock, references)
{
    protected override string NewReference() => "RQ-FIXED-0001";
}
```

Built over the same four collaborators, including a working `IReferenceGenerator`, `FixedReferenceEngine` returns quotes with the reference `"RQ-FIXED-0001"`. The two seams don't compete. The base `NewReference` delegates to the interface for everyday behavior, and a subclass can bypass it when a caller holds a concrete `RateEngine` it can't rebuild with a different generator but can still extend.

## Wrapping ambient dependencies behind injected abstractions

`DateTime.UtcNow` and `Guid.NewGuid()` are static members of base class library types you don't own: no constructor to parameterize, no method to override. Define an interface for what the ambient call returns, write a one-line implementation that forwards to the real value, and give the engine the interface:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

`IReferenceGenerator` and `GuidReferenceGenerator` wrap `Guid.NewGuid()` the same way. With the clock fixed to a literal `DateTimeOffset`, `QuotedAt` equals that exact instant, a precision the ambient call never allows; a fixed reference string lands unchanged in the quote.

.NET also ships `TimeProvider` for the clock seam, and a new project can start from `TimeProvider.System` instead of hand-rolling an interface. `IClock` is explicit here so that all four seams share one shape: an interface wrapping a collaborator or an ambient call.

## InternalsVisibleTo: a seam for a rule, not a dependency

`LaneValidation.IsKnownLane` isn't a collaborator the engine could get elsewhere. It's a fixed business rule, the three lanes the engine can quote, and `Quote` throws `UnknownLaneException` for anything else:

```csharp
internal static class LaneValidation
{
    internal static bool IsKnownLane(string laneCode) => laneCode is "ROT-NYC" or "SIN-LAX" or "HKG-FRA";
}
```

`internal` keeps it off the public surface: callers call `Quote` and handle the exception. The rule is still worth checking across more inputs than one exception path covers, and one line in the project file opens it to the tests' assembly:

```xml
<InternalsVisibleTo Include="RateEngineDependencies.Tests" />
```

The rule is then exercised both ways: directly, where `ROT-NYC`, `SIN-LAX` and `HKG-FRA` are known and `XXX-UNKNOWN` and the empty string aren't, and through the public behavior it governs, where an unknown lane throws `UnknownLaneException`.

{% include excalidraw.html name="42-fake-drives-the-engine" caption="Figure 42.3: A test hands four NSubstitute fakes into RateEngine's constructor, then verifies audit.Received(1).Record(...), the call the legacy engine's private list kept unreachable." %}

## Driving the dependency-broken engine with NSubstitute fakes

With four NSubstitute fakes in the constructor, each programmed with `Returns`, a test controls every value `Quote` depends on. A tariff of `4.25m` over 100 kilograms gives the expected amount; a fixed reference and the `quotedBy` argument land unchanged in the quote. Those checks confirm plumbing. The audit interaction is the one the legacy form made impossible to check:

```csharp
audit.Received(1).Record(Arg.Is<string>(message =>
    message.Contains("RQ-0002", StringComparison.Ordinal) &&
    message.Contains("C-9002", StringComparison.Ordinal)));
```

A `RateQuote` says nothing about whether `Quote` called `Record`, or what it recorded. `LegacyAuditLog.Record` appends to a private list nothing ever reads back, so the legacy engine leaves the question unanswerable. Because the refactored engine depends on `IAuditLog`, a substitute can play the audit log during the call and report afterward exactly what was recorded against it.

| Check | Style | Needs |
| --- | --- | --- |
| `audit.Received(1).Record(...)` | Interaction, verified on a fake | `IAuditLog` |
| `ListAuditLog.Entries` | State, inspected on a real log afterward | An inspectable implementation |

Both close the same gap, and both are closed to a type with no seam onto its audit log.

The production implementations still match the legacy numbers: `StandardTariffBook` returns the same three lane rates as `LegacyTariffBook.RateFor`, and `GuidReferenceGenerator.NewReference` returns a 32-character lowercase hex string. Breaking a dependency changes how a value is supplied, never what production computes.

## Build, run, observe

```sh
cd examples/42-dependency-breaking-techniques
dotnet test
```

All 14 tests pass: twelve drive the refactored engine's seams (tariff, audit, clock, reference, `quotedBy`, unknown-lane rejection, five lane codes through `InternalsVisibleTo`, and the overridden reference), and two cover the legacy engine's shape and the production implementations' values.

## When not to use it

These techniques get code under test before a change. They don't turn every seamless type into a pluggable platform. If the one change needed is a sixth lane code in a tariff table scheduled for deletion next release, breaking all five dependencies to land one test costs more than it buys. Extract only `ITariffBook`, leave the audit log, clock, reference generator and caller identity as they were, and ship.

`InternalsVisibleTo` needs the same restraint. It widens a library's internals to a named test assembly; it isn't a collaborator a production caller could supply. A test that calls `LaneValidation.IsKnownLane` is coupled to that method's name and signature, so changing the internal implementation can break a test that was never meant to see it.

| Use | When |
| --- | --- |
| `InternalsVisibleTo` | The thing to reach is a fixed rule like `IsKnownLane` |
| Extract Interface | A second implementation of the rule becomes plausible |

<blockquote class="ai-sidebar" markdown="1">
Asked to break a legacy method's dependencies, an AI assistant drafts an interface for a static lookup, parameterizes a constructor and extracts an overridable call about as fast as it reads the method. On a type like the legacy `RateEngine`, with five dependencies a few lines each to wrap, that's useful. What a draft can't settle is the smallest seam that gets today's change under test. An assistant asked to make a method testable tends to make all of it testable, every dependency broken, whether or not the change touches more than one. Whatever it proposes is safe only once the refactored engine returns the same quote as the legacy form for the same request, checked against both forms rather than assumed from how the new code reads. Scoping the seam and verifying behavior stay with you; drafting the seam is what the assistant does well.
</blockquote>

## For deeper coverage

- Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source catalogue for Extract Interface, Parameterize Constructor, Parameterize Method, and Extract and Override Call, the techniques applied to the rate-quoting engine.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the discipline of moving a method with baked-in dependencies to one with named collaborators in small, behavior-preserving steps.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection): constructor injection as .NET's own idiom for supplying collaborators like `ITariffBook` and `IAuditLog` from outside a type rather than letting the type construct them.
- Microsoft Learn, [InternalsVisibleToAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute): the attribute behind the seam onto `LaneValidation.IsKnownLane`.
- Microsoft Learn, [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider): the BCL's built-in abstraction over the system clock, a ready-made alternative to a hand-rolled `IClock` for a new seam.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
