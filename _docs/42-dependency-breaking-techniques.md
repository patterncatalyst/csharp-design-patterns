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

- Read `Legacy.RateEngine.Quote` and name the five dependencies it bakes in
  at the point of use: a newed-up `LegacyAuditLog`, the static
  `LegacyTariffBook.RateFor`, `Guid.NewGuid()`, `DateTime.UtcNow`, and
  `Environment.UserName`.
- Explain why a `sealed` class with a non-virtual `Quote` method closes off
  every subclass-based seam, and how
  `Legacy_engine_bakes_in_its_dependencies_with_no_seam` proves that shape
  through reflection rather than by asserting on an unpredictable value.
- Apply Extract Interface to the tariff lookup and the audit log
  (`ITariffBook`, `IAuditLog`), and Parameterize Constructor to collect all
  four of the refactored engine's collaborators into one constructor call.
- Distinguish Parameterize Method (`Quote(request, quotedBy)`) from
  Parameterize Constructor, and state which kind of dependency belongs on
  which seam: one holds for the engine's whole lifetime, the other is valid
  for exactly one call.
- Use Extract and Override Call on `NewReference` and `InternalsVisibleTo` on
  `LaneValidation.IsKnownLane` to open two further seams, neither of which
  needed a new interface, then drive the result with an NSubstitute fake
  that verifies the one audit interaction the legacy form made impossible
  to observe.

## Why this matters

Michael Feathers defines legacy code as code without tests. Not old code,
not badly written code: code nobody can change today with the confidence a
passing suite gives them about what just broke. By that definition, the
legacy `RateEngine` in this chapter is legacy the day it ships, not twenty
years later. Every dependency it needs (a place to look up a lane's rate, a
place to write an audit entry, a reference, a timestamp, and the caller's
identity) arrives through a call baked directly into the one method the
type exposes. Nothing about that method can be overridden, substituted, or
observed from outside, because nothing in the type was written with a test
in mind.

Getting a test onto code shaped like that before changing it hinges on
finding a seam: a point where a test can alter the program's behavior
without editing the exact place in the source where that behavior runs. In
C#, a seam is not automatic. A `sealed` class and a non-virtual method
decide, before a test is ever written, which kinds of seam are even
possible. That decision is not a defect to work around with a mocking
framework powerful enough to fake anything; it is a sequence of specific,
named techniques, Extract Interface, Parameterize Constructor, Parameterize
Method, Extract and Override Call, `InternalsVisibleTo`, and wrapping an
ambient call behind an injected abstraction, that take a method from "can
only run exactly as it runs in production" to "can be driven by a test that
controls every value the method depends on and inspects every interaction
the method has with the world outside it." None of these techniques change
what the engine computes. Every one of them changes how reachable that
computation is from a test, which is the one property this chapter's rate
engine starts without.

{% include excalidraw.html name="42-dependency-breaking-techniques" caption="Figure 42.1: The same rate-quoting engine shown twice: dependencies baked in at the call site on one side, broken out into constructor-injected and overridable seams on the other." %}

## A rate engine with five dependencies baked in

`Legacy.RateEngine.Quote` is short enough to read in one pass, and every
line reaches outside the method for something a test cannot control:

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

`new LegacyAuditLog()` means the only way this method gets an audit log is
to construct one for itself. A caller receives the `RateQuote` the method
returns and nothing else; the `LegacyAuditLog` instance, and whatever it
recorded, never leaves the method's local scope. The type's own
documentation states the consequence directly: nothing outside a
`LegacyAuditLog` instance, and nothing that constructed it, can ever read
its entries back. In the legacy engine, nothing outside the method even
gets to hold the instance in the first place.

`LegacyTariffBook.RateFor(request.LaneCode)` reaches a `switch` over lane
codes compiled directly into a static method on a static class. A test that
wants to see what this engine does when a tariff book returns an unusual
rate, a negative one, a zero one, one for a lane the switch does not
expect, has no argument, property, or constructor parameter to route that
rate through. The only way to change what `RateFor` returns is to edit
`LegacyTariffBook` itself.

`Guid.NewGuid().ToString("N")` and `DateTime.UtcNow` are both ambient:
values the runtime supplies from outside the program's own state, chosen
freshly on every call and impossible to predict from a test. An assertion
against the resulting `quote.Reference` or `quote.QuotedAt` has nothing
fixed to compare against; at best it can check a reference's length and
character set, or that a timestamp falls somewhere within a tolerance
window around "now," rather than asserting the exact value the engine
produced. `Environment.UserName` adds a fifth kind of dependency: the
identity of whichever account happens to be running the process, a fact
about the execution environment that has nothing to do with the freight
quote being computed and everything to do with who is computing it.

None of those five calls can be swapped out from a subclass, because
`Legacy.RateEngine` is `sealed`, and even if it were not, `Quote` is not
`virtual`. `LegacyContrastTests.Legacy_engine_bakes_in_its_dependencies_with_no_seam`
proves this shape structurally with reflection rather than by asserting on
one of the five unpredictable values the method produces: it asserts
`engineType.IsSealed`, that a public parameterless constructor exists (the
only kind of constructor this type offers), and that the `Quote` method
reflection returns has `IsVirtual` equal to `false`. The same test asserts
that `LegacyTariffBook`, a C# static class, compiles to a CLR type that is
simultaneously `IsAbstract` and `IsSealed`, confirming that a static class
has no instance seam at all, not even a constructor a test could decline to
call. Every fact that test checks is a closed door: sealed means no
subclass, non-virtual means no override even if a subclass existed, and an
abstract-sealed static type means no instance to substitute in the first
place. Feathers' catalogue of dependency-breaking techniques exists for
exactly this situation, a type with real production behavior worth keeping
and zero seams worth using as it stands.

## Extract Interface: naming the tariff lookup and the audit log

The first two seams come from naming an interface for exactly the members
the engine calls, and giving the engine that interface's type instead of
the concrete class's type. `ITariffBook` names the one method the engine
ever calls on a tariff book:

```csharp
public interface ITariffBook
{
    decimal RateFor(string laneCode);
}
```

`StandardTariffBook` implements it with the same per-lane rates
`LegacyTariffBook.RateFor` already returns, so the refactored engine quotes
identically to the legacy one over the same request; extracting the
interface changes how the rate is reached, not what rate comes back.
`IAuditLog` does the same for the audit sink:

```csharp
public interface IAuditLog
{
    void Record(string message);
}
```

`ListAuditLog` is the production implementation, and it fixes the one
defect that made `LegacyAuditLog` impossible to verify: it exposes an
`Entries` property, so any caller holding the instance, production code or
a test, can read back what was recorded. Extract Interface by itself only
buys a smaller surface to depend on. Pairing it with a production
implementation that is actually inspectable is what turns "the engine no
longer depends on one concrete audit log" into "a test can see what the
engine told the audit log."

## Parameterize Constructor: four collaborators in one call

Naming the interfaces does nothing until the engine stops constructing its
own collaborators and starts accepting them from outside. The refactored
`RateEngine` collects all four dependencies the legacy engine baked in
(minus the caller's identity, which belongs to a different seam) into one
primary constructor:

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

Parameterize Constructor moves the decision of what backs `tariffs`,
`audit`, `clock`, and `references` out of this type and onto whoever builds
an instance of it. Production code passes `StandardTariffBook`,
`ListAuditLog`, `SystemClock`, and `GuidReferenceGenerator`. A test passes
four NSubstitute fakes it fully controls. The engine's own code never
decides which; it only ever calls the four interface members it declared a
dependency on. This `RateEngine` is also not `sealed`, a second, equally
deliberate choice that keeps one more seam open for the Extract and
Override Call technique that follows.

## Parameterize Method: a caller identity that belongs to one call

`quotedBy` arrives as a method parameter, not a fifth constructor argument,
and that placement is not arbitrary. `tariffs`, `audit`, `clock`, and
`references` are each fixed for the whole lifetime of one `RateEngine`
instance: the same tariff book, the same audit sink, the same clock, and
the same reference generator back every quote that instance ever produces.
The caller's identity is not fixed the same way. Two different operators
can legitimately share one long-lived engine instance across two different
`Quote` calls, each one quoting on behalf of whoever is calling right now.
Baking `quotedBy` into the constructor would force a fresh engine per
caller, discarding the one instance's whole point, or a settable property a
second call could overwrite while a first call was still in flight.
Parameterize Method puts the value exactly where it varies: as an argument
on the one call it describes, replacing the ambient
`Environment.UserName` read with a value the caller supplies directly.
`Quote_carries_the_parameterized_quoted_by_value` asserts that
`quote.QuotedBy` equals whatever string a test passes as `quotedBy`,
proving the value flows straight through rather than being silently
replaced by anything ambient.

## Extract and Override Call: a seam that needs no interface

`NewReference` is a second, independent seam onto the same concern
`IReferenceGenerator` already covers, reachable without an interface at
all:

```csharp
protected virtual string NewReference() => references.NewReference();
```

Extract and Override Call pulls one call, here `references.NewReference()`,
into its own method, marks that method `protected virtual`, and leaves a
subclass free to replace the call entirely without touching the
constructor or the rest of `Quote`. `FixedReferenceEngine` in the test
project does exactly that:

```csharp
public sealed class FixedReferenceEngine(ITariffBook tariffs, IAuditLog audit, IClock clock, IReferenceGenerator references)
    : RateEngine(tariffs, audit, clock, references)
{
    protected override string NewReference() => "RQ-FIXED-0001";
}
```

`Extract_and_override_seam_replaces_the_reference_generator` builds a
`FixedReferenceEngine` over the same four substituted collaborators the
other tests use, including a working `IReferenceGenerator`, and asserts the
returned quote's reference is the literal `"RQ-FIXED-0001"` the override
returns rather than anything the injected generator would have produced.
This seam and the injected `IReferenceGenerator` are not competing
solutions to the same problem; the default `NewReference` implementation is
defined in terms of the interface, so the base class still depends on
`IReferenceGenerator` for its everyday behavior, and a subclass can bypass
that dependency entirely the moment a caller has a concrete `RateEngine` it
cannot rebuild with a different generator but can still extend.

## Wrapping ambient dependencies behind injected abstractions

`DateTime.UtcNow` and `Guid.NewGuid()` sit on sealed, static members of
Base Class Library types a reader does not own. There is no constructor on
`DateTime` or `Guid` to parameterize and no method on either to override,
because the call sites a test would need to reach live inside types this
book is not going to change. The fix applies the same move as Extract
Interface one level removed: define an interface for what the ambient call
returns, not for the call itself, write a one-line production
implementation that forwards to the real ambient value, and give the
engine the interface instead of the direct call.

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

`IReferenceGenerator` and `GuidReferenceGenerator` wrap `Guid.NewGuid()`
the same way. `Quote_stamps_the_injected_clock_time` fixes `clock.UtcNow`
to a literal `DateTimeOffset` and asserts `quote.QuotedAt` equals that
exact instant, a precision the ambient `DateTime.UtcNow` the legacy engine
reads could never support without an approximate-time window around
whenever the test happened to run. `Quote_uses_the_injected_reference_generator`
does the same for a fixed reference string instead of a generated Guid.
.NET also ships a built-in abstraction over the system clock,
`TimeProvider`, for exactly this seam; a new project reaching for a clock
seam today can start from `TimeProvider.System` directly rather than
hand-rolling an interface. This chapter defines `IClock` explicitly so that
the one interface wrapping an ambient BCL call reads the same way as the
other interfaces wrapping an owned collaborator, all four seams following
one shape.

## InternalsVisibleTo: a seam for a rule, not a dependency

`LaneValidation.IsKnownLane` is not a collaborator the engine could get
from somewhere else; it is a fixed business rule, the three lanes this
rate engine currently knows how to quote, backing the `UnknownLaneException`
`Quote` throws for anything else:

```csharp
internal static class LaneValidation
{
    internal static bool IsKnownLane(string laneCode) => laneCode is "ROT-NYC" or "SIN-LAX" or "HKG-FRA";
}
```

Marking it `internal` keeps it off the engine's public surface: no caller
outside this library needs to ask whether a lane code is known in advance,
they call `Quote` and handle `UnknownLaneException` if the answer was no.
A test, though, benefits from checking that rule directly across more
inputs than the one exception path conveniently exercises. The project file
names the test assembly in an `InternalsVisibleTo` entry, and that single
line opens a seam the engine's public API never needed to offer:

```xml
<InternalsVisibleTo Include="RateEngineDependencies.Tests" />
```

{% raw %}
```csharp
[Theory]
[InlineData("ROT-NYC", true)]
[InlineData("SIN-LAX", true)]
[InlineData("HKG-FRA", true)]
[InlineData("XXX-UNKNOWN", false)]
[InlineData("", false)]
public void Lane_validation_is_reachable_through_internals_visible_to(string laneCode, bool expectedKnown)
{
    Assert.Equal(expectedKnown, LaneValidation.IsKnownLane(laneCode));
}
```
{% endraw %}

`Quote_rejects_an_unknown_lane` checks the same rule from the public side,
asserting `UnknownLaneException` from a request naming a lane
`LaneValidation` does not recognize. Between the two tests, the rule is
exercised both directly, through the seam `InternalsVisibleTo` opens, and
indirectly, through the one public behavior that rule governs.

## Driving the dependency-broken engine with NSubstitute fakes

Every seam opened so far only pays off once a test actually uses it.
`DependencyBreakingTests` builds a `RateEngine` from four NSubstitute
fakes, one per constructor parameter, and programs each fake with
`Returns` before calling `Quote`:

```csharp
private static RateEngine BuildEngine(
    out ITariffBook tariffs,
    out IAuditLog audit,
    out IClock clock,
    out IReferenceGenerator references)
{
    tariffs = Substitute.For<ITariffBook>();
    audit = Substitute.For<IAuditLog>();
    clock = Substitute.For<IClock>();
    references = Substitute.For<IReferenceGenerator>();
    return new RateEngine(tariffs, audit, clock, references);
}
```

`Quote_uses_the_injected_tariff_book_rate` programs `tariffs.RateFor` to
return `4.25m` and asserts the resulting amount against a weight of 100
kilograms. `Quote_uses_the_injected_reference_generator` and
`Quote_carries_the_parameterized_quoted_by_value` each check that a value
supplied through a seam, the generator's return value or the `quotedBy`
argument, lands unchanged in the returned `RateQuote`. None of those
assertions is the interesting one, though; they confirm plumbing. The
audit interaction is the one the legacy form made structurally impossible
to check:

```csharp
audit.Received(1).Record(Arg.Is<string>(message =>
    message.Contains("RQ-0002", StringComparison.Ordinal) &&
    message.Contains("C-9002", StringComparison.Ordinal)));
```

A `RateQuote` carries an amount, a reference, a caller, and a timestamp. It
says nothing about whether `Quote` ever called `Record` at all, let alone
what message it recorded. `LegacyAuditLog.Record` appends to a private list
that nothing, not even the method that constructed the instance, ever
reads back; the legacy engine leaves that question permanently
unanswerable. The refactored engine answers it in one line against the
substitute itself: `audit.Received(1).Record(...)` verifies the exact
interaction, not a value the interaction happened to leave behind. That
line is only possible because `Quote` depends on `IAuditLog`, not on a
concrete `LegacyAuditLog`; a substitute built from the interface can both
play the audit log's role during the call and answer, once the call
returns, exactly what was recorded against it. `ListAuditLog.Entries`
offers a second, state-based way to check the same fact from a real audit
log instead of a fake one; the two approaches, inspecting state afterward
and verifying an interaction as it happens, cover the same gap from
different directions, and both are closed to a type with no seam onto its
audit log at all.

Two further tests round out the suite.
`Extract_and_override_seam_replaces_the_reference_generator` drives
`FixedReferenceEngine` through the same four fakes, confirming the
Extract-and-Override seam independently of whichever
`IReferenceGenerator` the constructor also received, and
`Standard_tariff_book_and_guid_reference_generator_behave` exercises the
two production implementations directly: `StandardTariffBook` returns the
same three lane rates `LegacyTariffBook.RateFor` always has, and
`GuidReferenceGenerator.NewReference` returns a 32-character lowercase hex
string. That last test matters as much as any fake-driven one: breaking a
dependency changes how a value is supplied, never what value production
code actually computes, and this test is the proof that the production
path through the refactored seams still produces the same numbers and the
same shape of reference the legacy, seamless engine always did.

## Build, run, observe

The example behind this chapter is
`examples/42-dependency-breaking-techniques`: the legacy `RateEngine` with
its five baked-in dependencies, the refactored `RateEngine` with its four
injected collaborators and its overridable reference seam, and the
`LaneValidation` rule reached through `InternalsVisibleTo`, all covered by
one test project. It is standalone-testable from its own directory:

```sh
cd examples/42-dependency-breaking-techniques
dotnet test
```

Observed locally with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 14
  failed: 0
  succeeded: 14
  skipped: 0
```

The fourteen cases split across two test classes. `DependencyBreakingTests`
accounts for twelve: six `Fact`s drive the refactored engine's seams one at
a time (the injected tariff rate, the audit interaction, the injected
clock, the injected reference generator, the parameterized `quotedBy`
value, and the unknown-lane rejection), one `Theory` runs the
`LaneValidation` rule across five lane codes through the
`InternalsVisibleTo` seam, and one further `Fact` drives
`FixedReferenceEngine` to confirm the Extract-and-Override seam
independently of the injected generator. `LegacyContrastTests` accounts
for the remaining two: a reflection-based proof that the legacy engine
offers no seam at all, and a direct check that `StandardTariffBook` and
`GuidReferenceGenerator`, the production implementations behind the
refactored engine's seams, compute the same values the legacy engine's
static calls always have.

## When not to use it

These techniques exist to get a piece of code under test before changing
it, not to turn every type that currently lacks a seam into a fully
pluggable platform regardless of whether anything downstream will ever
substitute, override, or verify what sits behind one. If the one change
this rate engine needs is a sixth lane code added to a tariff table that is
scheduled for deletion in the next release anyway, breaking all five
dependencies to land one test for that single addition costs more than it
buys. The smallest seam that supports the change actually coming, here,
extracting only `ITariffBook` and leaving the audit log, the clock, the
reference generator, and the caller's identity exactly as the legacy form
left them, ships faster and risks less than redoing the whole engine's
dependency graph for a change nobody asked for.

`InternalsVisibleTo` deserves the same restraint. It widens a library's
internal surface to a named test assembly, not to a substitutable
collaborator a production caller could also choose to supply. A test that
calls `LaneValidation.IsKnownLane` directly is coupled to that method's
exact name and signature in a way a test built against `ITariffBook` is
never coupled to `StandardTariffBook`; changing the internal
implementation, even for a good reason, can break a test that was never
supposed to see it. Reach for `InternalsVisibleTo` when the thing a test
needs to reach is a fixed rule like this one, not a dependency with more
than one production shape, and reach for Extract Interface the moment a
second implementation of that rule becomes plausible.

<blockquote class="ai-sidebar" markdown="1">
Asked to break a legacy method's dependencies, an AI assistant can draft an
interface for a static lookup, parameterize a constructor, and extract an
overridable call about as fast as it can read the method once. That speed
is real help on a type like the legacy `RateEngine`, five dependencies,
each one a few lines to name and wrap. What a draft cannot settle on its
own is which seam is the smallest one that gets today's actual change under
test. An assistant asked to make a method testable tends to make all of it
testable: every dependency broken and every ambient call wrapped, whether
or not the change at hand touches more than one of them. And whichever
shape it proposes, that proposal is only safe once something confirms the
refactored engine still returns the same quote for the same request the
legacy form returned before anyone touched it, a check run directly against
both forms rather than assumed from how the new code reads. Scoping a seam
to the change in front of you, and verifying behavior survived the edit,
stays a human call; drafting the seam itself is the part an assistant does
well.
</blockquote>

## For deeper coverage

- Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source catalogue for Extract Interface, Parameterize Constructor, Parameterize Method, and Extract and Override Call, the techniques this chapter applies to the same rate-quoting engine.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the discipline of moving a method with baked-in dependencies to one with named collaborators in small, behavior-preserving steps.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection): constructor injection as .NET's own idiom for supplying collaborators like `ITariffBook` and `IAuditLog` from outside a type rather than letting the type construct them.
- Microsoft Learn, [InternalsVisibleToAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute): the attribute behind the seam onto `LaneValidation.IsKnownLane`.
- Microsoft Learn, [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider): the BCL's built-in abstraction over the system clock, a ready-made alternative to a hand-rolled `IClock` for a new seam.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
