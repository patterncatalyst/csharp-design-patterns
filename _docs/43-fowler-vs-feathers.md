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

- State in one sentence each what Martin Fowler's *Refactoring* catalogue and Michael Feathers' *Working Effectively with Legacy Code* catalogue are for, and why they run in sequence on the same method.
- Identify the three traits of `LegacyRateEngine.Quote` that close every seam: a sealed class, a non-virtual method and a hard-coded `new LegacyTariffTable()`.
- Apply the minimal Feathers seam, constructor injection of `ITariffSource` and `IClock`, without touching the method's signature, `switch` or literals.
- Name four Fowler refactorings, Extract Method, Replace Conditional with Polymorphism, Introduce Parameter Object and Replace Magic Literal, and point to the type each produces.
- Prove a refactor preserved behavior by running the seamed and refactored engines side by side.

## Why this matters

Two books get cited around legacy code for different reasons that are easy to blur:

| Catalogue | Problem it solves | Assumes |
| --- | --- | --- |
| Fowler, *Refactoring* | Improving a design in small, named, reversible, behavior-preserving steps | Tests already protect the code |
| Feathers, *Working Effectively with Legacy Code* | Getting code under test when it was never written for it | Nothing; it creates the seam |

A **seam** is a place where a test can change a program's behavior without editing the line that behavior runs on. A sealed class with no injected collaborators has none, and Fowler's catalogue assumes one exists.

So the order is fixed. A method with no seam can't be refactored safely, because no test catches a step that changes behavior. Extract Method on code like that is editing production logic without a net. Feathers first: open the smallest seam that makes the code testable. Then Fowler: apply named refactorings behind the tests the seam allows. One rate-quoting engine goes through three shapes: the untestable original, the minimally seamed form and the fully refactored form.

{% include excalidraw.html name="43-fowler-vs-feathers" caption="Figure 43.1: A sealed legacy engine gains a constructor seam, then a Fowler refactor reshapes its internals, both catalogues solving a different problem on the same engine." %}

## The legacy engine: a sealed class with no seam

`LegacyRateEngine` sits under a `.Legacy` namespace as a contrast, not a model. `Quote` takes three loose primitives and returns a decimal:

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

Three facts matter more than the arithmetic:

- `new LegacyTariffTable()` builds the lookup on every call, and its constructor always seeds `"STD"` at the same rate, so a test can't supply another rate.
- `DateTime.Now.Hour` reads the wall clock, so a peak-surcharge check either waits for 08:00 or 18:00 or exercises its branch only sometimes.
- `LegacyRateEngine` is `sealed` and `Quote` isn't `virtual`, which rules out subclassing around the first two.

Reflection confirms the shape without touching `DateTime.Now`: the type is sealed, has a single parameterless constructor, and `Quote` isn't virtual. Each fact closes a different door. The method has production behavior worth keeping and no seam to drive it from a test, which is Feathers' territory.

## The Feathers stage: the smallest seam that makes it testable

What's the smallest change that lets a test control everything the method depends on? Two constructor parameters:

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

Almost nothing else moved. The loose `(decimal, decimal, string)` signature, the inline `switch` on `"EXPRESS"` and `"OVERNIGHT"`, the `0.015m` literal and the hard-coded `"STD"` are all still there. `ITariffSource` replaces `new LegacyTariffTable()`, and `IClock` replaces `DateTime.Now`, behind `PeakWindow.IsPeak` so the peak rule reads the same in both later engines. That's the whole diff. A seam opens a door without redesigning the room.

The restraint is the easiest part to get wrong under time pressure. Looking at an inline `switch` or a magic `0.015m`, the instinct is to fix it while you're in the file. Feathers' advice runs the other way: get a test in place on the structure as it stands. A seam that also redesigns the method can change behavior before any test exists to catch it.

{% include excalidraw.html name="43-feathers-then-fowler" caption="Figure 43.2: Feathers techniques open a seam so the untested engine can run under test, then Fowler techniques reshape its internals while every test stays green." %}

## The Fowler stage: refactoring behind the tests the seam made possible

With `SeamedRateEngine` under test, the next question becomes safe to ask: what's the clearest shape for this method? Every change from the seamed form to `RateEngine` is a named Fowler refactoring.

| Refactoring | Produces |
| --- | --- |
| Extract Method | `BaseCharge`, `WeightCharge`, `PeakSurcharge` |
| Replace Conditional with Polymorphism | `IServiceSurcharge` and one class per `ServiceLevel` |
| Introduce Parameter Object | `RateRequest` |
| Replace Magic Literal | `RateRules` |

### Extract Method

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

Each extracted method names what it computes, and `Quote` reads as a short sequence of steps: base charge, weight charge, service surcharge, peak adjustment, quote. The arithmetic didn't change; only the labels did.

### Replace Conditional with Polymorphism

`IServiceSurcharge` names the one operation every service level needs, and one small class implements it per `ServiceLevel`:

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

`ServiceSurcharges.For` is the one place left that switches, mapping a level to its handler:

```csharp
public static IServiceSurcharge For(ServiceLevel level) => level switch
{
    ServiceLevel.Standard => new StandardSurcharge(),
    ServiceLevel.Express => new ExpressSurcharge(),
    ServiceLevel.Overnight => new OvernightSurcharge(),
    _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown service level."),
};
```

The difference is scope. The legacy `switch` mixed choosing a surcharge with applying it, in the same method that computes the base charge and reads the clock. `ServiceSurcharges.For` only selects. A fourth service level means one class and one case here, and `RateEngine.Quote` doesn't change.

### Introduce Parameter Object

The loose signature becomes one typed value:

```csharp
public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, ServiceLevel Service);
```

A `ServiceLevel` enum replaces the bare string, so a misspelling like `"EXPRES"` or `"express"` no longer falls through the `switch` and quotes with no surcharge: it doesn't compile as a `ServiceLevel` member, and an out-of-range value cast to the enum hits `ServiceSurcharges.For`'s `ArgumentOutOfRangeException`. The `ConsignmentId` the legacy signature never carried rides on the same record, because a quote needs a consignment to attach to.

### Replace Magic Literal

```csharp
internal static class RateRules
{
    internal const decimal DeclaredValueRate = 0.015m;
    internal const decimal PeakMultiplier = 0.1m;
    internal const string StandardLane = "STD";
}
```

`0.015m` was unlabeled at its one use in both earlier engines. `RateRules.DeclaredValueRate` is the same value with a name and one place to change it. `RateRules` is `internal` because nothing outside the library needs it; a test reaches it, and `PeakWindow.IsPeak`, through `InternalsVisibleTo`, so the peak boundary can be checked across a range of hours directly.

Each of the four is reversible on its own, each left the numbers unchanged, and the tests the seam made possible are why that claim can be trusted.

## Proving the refactor preserved behavior

A refactoring that changes output is an undocumented behavior change under a refactoring's name. "`RateEngine` computes what `SeamedRateEngine` computed" needs an executable check, not a comment.

One theory feeds both engines the same `StandardTariffSource` and the same `FakeClock` instant, maps the request's `ServiceLevel` back to the legacy string for the seamed engine, and compares the two amounts. Its eight cases cover all three service levels at a peak and an off-peak instant, plus a zero-value, zero-weight consignment and a small fractional-weight one. All eight agree.

A worked case: 1000.00 declared and 200 kg, off peak, standard service. The base charge is `1000.00 * 0.015` = 15.00 and the weight charge `200.00 * 2.40` = 480.00, for 495.00. `SeamedRateEngine` computes that in one method body; `RateEngine` computes it across `BaseCharge`, `WeightCharge`, `ServiceSurcharges.For(ServiceLevel.Standard)` and `PeakSurcharge`. Both give 495.00. Fixed values pin `RateEngine` on its own too:

| Case | Quote |
| --- | --- |
| Standard, off peak | 495.00 |
| Standard, peak | 544.50 |
| Express, off peak | 618.75 |
| Overnight, off peak | 742.50 |

A change to any extracted piece fails against a fixed number immediately. Feathers' seam made the engine observable. Fowler's refactorings reshaped what was observed. The equivalence theory shows the second step changed the engine's shape, names, surcharge polymorphism and request type, and none of its behavior.

{% include excalidraw.html name="43-behaviour-preserved" caption="Figure 43.3: One request and one fixed clock feed both the seamed and the refactored engine, and a theory test asserts their amounts match across every service level and clock instant." %}

## Build, run, observe

```sh
cd examples/43-fowler-vs-feathers
dotnet test
```

All 24 tests pass, from eleven test methods: the reflection check on the legacy engine, the fixed quotes and surcharges, the clock stamp, a substituted `ITariffSource`, theories over the three service levels and five clock hours, the standard tariff rate, and the eight-case equivalence theory. All three engines use the same `0.015m` rate and the same `"STD"` lane at 2.40 per kilogram.

## When not to use it

Both catalogues cost effort, so both deserve restraint. If `LegacyRateEngine.Quote` needs one small change, say a new service level in the `switch`, before it's retired in a release or two, injecting both collaborators and running all four refactorings costs more than the change justifies. Seam only the collaborator the change touches, leave the rest as it stands, and skip the polymorphic surcharge until a second or third variant makes the `switch` the bottleneck. Running the full catalogue on code about to be deleted is effort on a design nobody will read again.

Order matters as much as restraint. Extract Method or Replace Conditional with Polymorphism on a method still shaped like `LegacyRateEngine`, sealed, non-virtual, with a hard-coded collaborator, edits production logic with nothing to catch a mistake. The seam comes first, green, before any Fowler technique touches the method.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a legacy change.** "Clean up `LegacyRateEngine`" doesn't say whether the method gets a seam before it's refactored, or which refactoring the change needs. State the order:

> First, the Feathers step: copy `LegacyRateEngine.Quote` into `SeamedRateEngine` with the body unchanged, replacing only `new LegacyTariffTable()` with an `ITariffSource` and `DateTime.Now` with an `IClock`, both through the constructor. Second, add an equivalence theory that runs `SeamedRateEngine` and `RateEngine` on the same `RateRequest` and `FakeClock` instant for every `ServiceLevel`, at peak and off peak. Third, the Fowler step: in `RateEngine`, replace the service-level `switch` with `ServiceSurcharges.For` and move the literals into `RateRules`. Don't start the third step until the second passes.

Run `dotnet test --project RateEngineCatalogues.Tests --filter "EquivalenceTests|FowlerFeathersTests"`; all must pass.
</blockquote>

## For deeper coverage

- Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source catalogue for the seam opened on `SeamedRateEngine`, and for the idea that legacy code means code without tests, not code of any particular age.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the source catalogue for Extract Method, Replace Conditional with Polymorphism, Introduce Parameter Object, and Replace Magic Literal, the four refactorings applied to the seamed engine.
- Microsoft Learn, [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching): the switch expression behind `ServiceSurcharges.For` and `PeakWindow.IsPeak`'s relational pattern.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the record type behind `RateRequest` and `RateQuote`, and why a record is the idiomatic shape for a parameter object with no identity beyond its values.
- Microsoft Learn, [InternalsVisibleToAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute): the attribute behind the tests' direct access to `RateRules` and `PeakWindow`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
