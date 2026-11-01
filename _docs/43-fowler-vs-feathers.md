---
title: "Fowler vs Feathers, contrasted and combined"
order: 43
layout: tutorial
sectionid: docs
part: "changing-inherited-code"
description: "The refactoring catalogue and the dependency-breaking catalogue, contrasted and combined on one engine."
---

<!-- EXAMPLE (shipping domain): The same engine under Fowler and under Feathers. -->

## Learning objectives

By the end of this chapter you can:

- State the difference between Martin Fowler's *Refactoring* catalogue and
  Michael Feathers' *Working Effectively with Legacy Code* catalogue in one
  sentence each, and explain why the two run in sequence rather than in
  competition on the same method.
- Read `LegacyRateEngine.Quote` and identify the three traits, a sealed
  class, a non-virtual method, and a hard-coded `new LegacyTariffTable()`,
  that close off every Feathers seam before a single refactoring can begin.
- Apply the minimal Feathers seam, constructor injection of `ITariffSource`
  and `IClock`, to `SeamedRateEngine` without changing its loose
  `(decimal, decimal, string)` signature, its inline `switch`, or its inline
  rate literals.
- Name four Fowler refactorings, Extract Method, Replace Conditional with
  Polymorphism, Introduce Parameter Object, and Replace Magic Literal, and
  point to the exact type each one produces in the fully refactored
  `RateEngine`.
- Explain what `EquivalenceTests.Both_catalogues_produce_the_same_quote`
  proves about the relationship between `SeamedRateEngine` and `RateEngine`,
  and why that proof has to run as an executable test rather than stand as a
  claim in a comment.

## Why this matters

Two books get cited constantly around legacy code, and they get cited for
different reasons that are easy to blur together. Martin Fowler's
*Refactoring* is a catalogue of small, behavior-preserving steps for
improving a design once it already has tests to protect it: Extract Method,
Replace Conditional with Polymorphism, Introduce Parameter Object, each one
named, each one reversible, each one safe to run one at a time behind a
green suite. Michael Feathers' *Working Effectively with Legacy Code* is a
different catalogue, aimed one step earlier: how to get a piece of
production code under test in the first place, when the code was never
written with a seam in mind. Feathers' name for that earlier problem is a
seam, a place where a test can change a program's behavior without editing
the exact line the behavior runs on. A sealed class with no injected
collaborators has no seam. Fowler's whole catalogue assumes one already
exists.

That ordering is the entire point of this chapter. A method with no seam
cannot be refactored safely, because there is no test to catch a refactoring
that accidentally changes behavior instead of preserving it. Reaching
straight for Extract Method or Replace Conditional with Polymorphism on
code like that is not refactoring in Fowler's sense; it is editing
production logic with no net under the wire. The correct order is Feathers
first, open the smallest seam that makes the code testable, then Fowler,
apply the named refactorings behind the tests that seam makes possible. One
rate-quoting engine walks through all three shapes this chapter needs:
the untestable original, the minimally seamed form, and the fully
refactored form, with an executable test proving the last one did not
quietly change what the engine computes along the way.

{% include excalidraw.html name="43-fowler-vs-feathers" caption="Figure 43.1: A sealed legacy engine gains a constructor seam, then a Fowler refactor reshapes its internals, both catalogues solving a different problem on the same engine." %}

## The legacy engine: a sealed class with no seam

`LegacyRateEngine` lives under a `.Legacy` namespace specifically so its
shape reads as a contrast, not as a model to imitate. Its `Quote` method
takes three loose primitives and returns a decimal:

```csharp
public decimal Quote(decimal declaredValue, decimal weightKilograms, string serviceLevel)
{
    var tariff = new LegacyTariffTable();
    var subtotal = declaredValue * 0.015m;
    subtotal += weightKilograms * tariff.RatePerKilogram("STD");

    switch (serviceLevel)
    {
        case "EXPRESS":
            subtotal *= 1.25m;
            break;
        case "OVERNIGHT":
            subtotal *= 1.5m;
            break;
    }

    if (DateTime.Now.Hour is >= 8 and < 18)
    {
        subtotal *= 1.1m;
    }

    return subtotal;
}
```

Three facts about this method matter more than the arithmetic. First,
`new LegacyTariffTable()` builds its own tariff lookup on every call; a test
wanting to see what the engine does with a different per-kilogram rate has
no way to supply one, because `LegacyTariffTable`'s constructor always seeds
the same `"STD"` lane at the same rate. Second, `DateTime.Now.Hour` reads
the ambient wall clock directly, so a test that wants to assert the peak
surcharge has to either wait for the real clock to cross 08:00 or 18:00, or
settle for an assertion that only sometimes exercises the branch it is
supposed to check. Third, `LegacyRateEngine` is `sealed` and `Quote` is not
`virtual`, which closes off even the fallback option of subclassing the
engine to override its way around the first two problems.

`LegacyContrastTests.Legacy_engine_offers_no_seam_before_feathers_opens_one`
proves this structurally, through reflection, rather than by asserting
against the unpredictable value `DateTime.Now` would produce:

```csharp
var legacyEngineType = typeof(Legacy.LegacyRateEngine);

Assert.True(legacyEngineType.IsSealed);

var constructors = legacyEngineType.GetConstructors();
var constructor = Assert.Single(constructors);
Assert.Empty(constructor.GetParameters());

var quoteMethod = legacyEngineType.GetMethod("Quote", BindingFlags.Public | BindingFlags.Instance);
Assert.NotNull(quoteMethod);
Assert.False(quoteMethod.IsVirtual);
```

Every fact that assertion checks closes a different door: sealed means no
subclass, a single parameterless constructor means no collaborator can be
substituted at construction time, and a non-virtual method means no
override even if the sealed modifier were lifted. This is Feathers'
territory exactly: a method with real, worth-keeping production behavior
and zero seams to drive it from a test.

## The Feathers stage: the smallest seam that makes it testable

`SeamedRateEngine` is the answer to one narrow question: what is the
smallest change that lets a test control everything this method depends on,
without touching anything else about how the method is written? The answer
turns out to be two constructor parameters:

```csharp
public sealed class SeamedRateEngine(ITariffSource tariffs, IClock clock)
{
    public decimal Quote(decimal declaredValue, decimal weightKilograms, string serviceLevel)
    {
        var subtotal = declaredValue * 0.015m;
        subtotal += weightKilograms * tariffs.RatePerKilogram("STD");

        switch (serviceLevel)
        {
            case "EXPRESS":
                subtotal *= 1.25m;
                break;
            case "OVERNIGHT":
                subtotal *= 1.5m;
                break;
        }

        if (PeakWindow.IsPeak(clock.UtcNow))
        {
            subtotal *= 1.1m;
        }

        return subtotal;
    }
}
```

Compare this line by line against `LegacyRateEngine.Quote` and almost
nothing else moved. The loose `(decimal, decimal, string)` signature is
still there. The inline `switch` on `"EXPRESS"` and `"OVERNIGHT"` is still
there. The literal `0.015m` ad-valorem rate is still there. Even the
hard-coded `"STD"` lane code passed to `RatePerKilogram` is still there.
`ITariffSource` replaces `new LegacyTariffTable()`, and `IClock` replaces
`DateTime.Now`, wrapped behind `PeakWindow.IsPeak` so the peak-hour rule
reads the same way from both the seamed and the refactored engine. That is
the entire diff. Feathers calls this kind of change a seam specifically
because it opens a door without redesigning the room: nothing about the
method's structure, its parameter shape, or its conditional logic was
judged or improved, because judging and improving structure is not what
this stage is for. The job here is strictly "make it reachable from a
test," and `SeamedRateEngine` does exactly that and nothing more.

That restraint is deliberate, and it is also the easiest thing to get wrong
under time pressure. The instinct, looking at an inline `switch` or a magic
`0.015m`, is to fix it while you are already in the file. Feathers' own
advice runs the other way: get the test in place first, on the structure as
it stands, however rough that structure is. A seam that also tries to
redesign the method at the same time risks changing behavior before any
test exists to catch the change, which defeats the entire reason for
seaming the method in the first place.

{% include excalidraw.html name="43-feathers-then-fowler" caption="Figure 43.2: Feathers techniques open a seam so the untested engine can run under test, then Fowler techniques reshape its internals while every test stays green." %}

## The Fowler stage: refactoring behind the tests the seam made possible

With `SeamedRateEngine` under test, the second question becomes available
for the first time: what does this method do, and what is its
clearest shape? `RateEngine` is the answer, and every change from the
seamed form to this one is a named Fowler refactoring, not a redesign from
scratch.

**Extract Method** pulls the subtotal's two components and the peak
surcharge into their own named pieces:

```csharp
public RateQuote Quote(RateRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    var subtotal = BaseCharge(request) + WeightCharge(request);
    subtotal = ServiceSurcharges.For(request.Service).Apply(subtotal);

    var at = clock.UtcNow;
    var total = subtotal + PeakSurcharge(subtotal, at);

    return new RateQuote(request.ConsignmentId, total, at);
}

private static decimal BaseCharge(RateRequest request) => request.DeclaredValue * RateRules.DeclaredValueRate;

private decimal WeightCharge(RateRequest request) => request.WeightKilograms * tariffs.RatePerKilogram(RateRules.StandardLane);

private static decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) =>
    PeakWindow.IsPeak(at) ? subtotal * RateRules.PeakMultiplier : 0m;
```

`BaseCharge` and `WeightCharge` each say what they compute in their own
name, instead of leaving a reader to infer it from an unlabeled line inside
a longer method. `Quote` itself now reads as a short sequence of named
steps: a base charge, a weight charge, a surcharge, a peak adjustment, a
returned quote. None of the arithmetic changed; only the labels around it
did.

**Replace Conditional with Polymorphism** removes the inline `switch`
entirely. `IServiceSurcharge` names the one operation every service level
needs, `Apply`, and three small classes each implement it for exactly one
`ServiceLevel`:

```csharp
public interface IServiceSurcharge
{
    decimal Apply(decimal subtotal);
}

public sealed class ExpressSurcharge : IServiceSurcharge
{
    public decimal Apply(decimal subtotal) => subtotal * 1.25m;
}
```

`ServiceSurcharges.For(ServiceLevel)` is the one place left that still
switches on a value, mapping a `ServiceLevel` to the `IServiceSurcharge` that
handles it:

```csharp
public static IServiceSurcharge For(ServiceLevel level) => level switch
{
    ServiceLevel.Standard => new StandardSurcharge(),
    ServiceLevel.Express => new ExpressSurcharge(),
    ServiceLevel.Overnight => new OvernightSurcharge(),
    _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown service level."),
};
```

The difference between this `switch` and the one it replaced is scope.
`LegacyRateEngine`'s inline `switch` mixed the decision of which surcharge
applies with the arithmetic of applying it, in the middle of the method
that also computes the base charge and reads the clock. `ServiceSurcharges.For`
does exactly one job, selection, and hands the arithmetic off to a type
built for nothing else. Adding a fourth service level later means adding
one class and one case here, never touching `RateEngine.Quote` again.

**Introduce Parameter Object** replaces the loose
`(decimal declaredValue, decimal weightKilograms, string serviceLevel)`
signature with one typed value:

```csharp
public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, ServiceLevel Service);
```

A `RateRequest` carries a `ServiceLevel` enum member instead of a bare
string, so `"EXPRES"` or `"express"` can no longer silently fall through the
legacy engine's `switch` and quote as if no surcharge applied at all; the
compiler rejects anything that is not one of the three declared
`ServiceLevel` values before the request ever reaches `Quote`. The
`ConsignmentId` the legacy signature never carried also rides along on the
same object, because a quote without a consignment identity to attach it to
is not a complete request in the first place.

**Replace Magic Literal** gives every inline number and string in the
seamed engine a name:

```csharp
internal static class RateRules
{
    internal const decimal DeclaredValueRate = 0.015m;
    internal const decimal PeakMultiplier = 0.1m;
    internal const string StandardLane = "STD";
}
```

`0.015m` was the ad-valorem rate in both the legacy and the seamed engine,
unlabeled at its one use site in each. `RateRules.DeclaredValueRate` is the
same value with a name a reader does not have to infer from context, and a
single place to change it if the rate ever moves. `RateRules` is marked
`internal`, not `public`, because nothing outside this library has a
legitimate reason to read these constants directly; a test reaches them
through `InternalsVisibleTo`, the same narrow seam that also exposes
`PeakWindow.IsPeak` for direct assertion across a range of hours rather than
only through the one public code path that happens to call it.

Four refactorings, applied to one method that was already under test
because the Feathers stage put it there first. Each one is reversible on
its own, each one left the method computing the same numbers it computed
before, and the test suite that already existed is the thing that lets a
reader trust that claim instead of just hoping it.

## Proving the refactor preserved behavior

A refactoring that quietly changes an engine's output is not a refactoring;
it is an undocumented behavior change wearing a refactoring's name. The
claim this chapter has been making all along, that `RateEngine` computes the
same quote `SeamedRateEngine` always computed, is exactly the kind of claim
that needs an executable check rather than a comment asserting it.
`EquivalenceTests.Both_catalogues_produce_the_same_quote` is that check:

```csharp
[Theory]
[MemberData(nameof(RequestsAndClockInstants))]
public void Both_catalogues_produce_the_same_quote(RateRequest request, DateTimeOffset at)
{
    var tariffs = new StandardTariffSource();
    var clock = new FakeClock(at);
    var seamed = new SeamedRateEngine(tariffs, clock);
    var refactored = new RateEngine(tariffs, clock);
    var serviceLevel = request.Service switch
    {
        ServiceLevel.Express => "EXPRESS",
        ServiceLevel.Overnight => "OVERNIGHT",
        _ => "STANDARD",
    };

    var seamedAmount = seamed.Quote(request.DeclaredValue, request.WeightKilograms, serviceLevel);
    var refactoredAmount = refactored.Quote(request).Amount;

    Assert.Equal(seamedAmount, refactoredAmount);
}
```

One theory, eight cases, built from the same `StandardTariffSource` and the
same `FakeClock` instant feeding both engines side by side. The cases cover
all three `ServiceLevel` values crossed with a peak and an off-peak instant,
plus two edge requests, a zero-value, zero-weight consignment and a small
fractional-weight one, that exercise the arithmetic away from the round
numbers the other tests use. A worked example makes the equivalence
concrete: a declared value of 1000.00 and a weight of 200 kilograms, off
peak, on the standard service level, runs `1000.00 * 0.015` for a base
charge of 15.00 and `200.00 * 2.40` for a weight charge of 480.00, summing
to 495.00. `SeamedRateEngine` computes that total inline in one method body;
`RateEngine` computes it across `BaseCharge`, `WeightCharge`,
`ServiceSurcharges.For(ServiceLevel.Standard)`, and `PeakSurcharge`, and
both arrive at the identical 495.00. `FowlerFeathersTests` locks down the
same number directly against `RateEngine` alone, alongside 544.50 for the
peak-surcharged case, 618.75 for Express, and 742.50 for Overnight, so a
future change to any of the extracted pieces has a fixed value to fail
against immediately rather than only when the equivalence theory happens to
notice.

This is the payoff the whole chapter has been building toward. Feathers'
seam made the engine observable from a test. Fowler's refactorings reshaped
what the test was observing. The equivalence theory is the proof that the
second step never silently invalidated the thing the first step was for:
the refactor changed the engine's shape, the names of its methods, the
polymorphism behind its surcharge, the type behind its request, and it
changed none of its behavior, the one property a test can check.

{% include excalidraw.html name="43-behaviour-preserved" caption="Figure 43.3: One request and one fixed clock feed both the seamed and the refactored engine, and a theory test asserts their amounts match across every service level and clock instant." %}

## Build, run, observe

The example behind this chapter is `examples/43-fowler-vs-feathers`: the
untestable `LegacyRateEngine` under `.Legacy`, the minimally seamed
`SeamedRateEngine`, and the fully refactored `RateEngine`, all three built
from the same `0.015m` ad-valorem rate and the same `"STD"` lane at
2.40 per kilogram. It is standalone-testable from its own directory:

```sh
cd examples/43-fowler-vs-feathers
dotnet test
```

Observed locally with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 24
  failed: 0
  succeeded: 24
  skipped: 0
```

The twenty-four cases come from eleven test methods across three classes.
`LegacyContrastTests` contributes one reflection-based fact proving the
legacy engine has no seam at all. `FowlerFeathersTests` contributes nine
methods: six `Fact`s pin a fixed quote, the peak surcharge, the Express and
Overnight surcharges, the injected clock's stamp on a returned quote, and a
substituted `ITariffSource` built with NSubstitute, plus two `Theory`
methods, one across all three `ServiceLevel` values for the polymorphic
surcharge selection and one across five clock hours for the
`InternalsVisibleTo` seam onto `PeakWindow.IsPeak` and the `RateRules`
constants, and one further `Fact` confirming `StandardTariffSource` returns
the same rate the legacy table always seeded. `EquivalenceTests` contributes
the one remaining method, the eight-case theory proving `SeamedRateEngine`
and `RateEngine` agree on every request it drives through both.

## When not to use it

Opening a seam is not free, and neither is a Fowler refactoring, so both
catalogues deserve the same restraint. If a legacy method like
`LegacyRateEngine.Quote` only needs one small, well-understood change, say,
a new service level appended to the inline `switch`, before it is retired
in a release or two anyway, injecting `ITariffSource` and `IClock` purely to
get that one change under test, then running all four Fowler refactorings
on top of the new seam, spends more effort than the change in front of you
justifies. The Feathers move that fits that situation might be
narrower: seam only the collaborator the change touches, leave the rest of
the method exactly as it stands, and skip the polymorphic surcharge
extraction entirely until a second or third service level variant makes the
inline `switch` itself the bottleneck. Running the full catalogue on code
that is about to be deleted is effort spent on a design nobody will read
again.

The order matters as much as the restraint. Reaching for Extract Method or
Replace Conditional with Polymorphism on a method that still has
`LegacyRateEngine`'s shape, sealed, non-virtual, a hard-coded collaborator,
is not safe refactoring; it is an edit to production logic with no test to
catch a mistake. The seam always comes first, confirmed green, before any
Fowler technique touches the same method.

<blockquote class="ai-sidebar" markdown="1">
An AI assistant can name a Feathers seam or a Fowler refactoring about as
fast as it can read the method you point it at: suggest extracting
`ITariffSource`, propose a polymorphic surcharge hierarchy in place of a
`switch`, draft the `RateRules` constants a magic literal is hiding behind.
That speed helps most on a method shaped like `LegacyRateEngine.Quote`,
where the problems are structural and easy to name once pointed out. What
an assistant's suggestion cannot settle on its own is the order of
operations: whether the method already has a seam worth trusting before any
refactoring runs, and whether the refactoring it is proposing is the one
the current change needs or simply the most thorough one available.
Getting the code under test before reshaping it, and judging which
refactoring earns its cost against today's change rather than against an
idealized design, stays a human call; drafting the seam or the refactoring
once that call is made is the part an assistant does well.
</blockquote>

## For deeper coverage

- Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source catalogue for the seam this chapter opens on `SeamedRateEngine`, and for the idea that legacy code means code without tests, not code of any particular age.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the source catalogue for Extract Method, Replace Conditional with Polymorphism, Introduce Parameter Object, and Replace Magic Literal, the four refactorings this chapter applies to the seamed engine.
- Microsoft Learn, [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching): the switch expression behind `ServiceSurcharges.For` and `PeakWindow.IsPeak`'s relational pattern.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the record type behind `RateRequest` and `RateQuote`, and why a record is the idiomatic shape for a parameter object with no identity beyond its values.
- Microsoft Learn, [InternalsVisibleToAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute): the attribute behind the test project's direct access to `RateRules` and `PeakWindow`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
