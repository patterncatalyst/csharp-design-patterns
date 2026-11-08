---
title: "Anti-patterns and pattern misuse"
order: 40
layout: tutorial
sectionid: docs
part: "patterns-in-the-wild"
description: "Service Locator, over-abstraction, and pattern-shaped LLM output: patterns applied past the point they pay for themselves."
---

<!-- EXAMPLE (shipping domain): The over-abstracted rating engine. -->

## Learning objectives

By the end of this chapter you can:

- Compute a freight rate quote in one static method and say why it needs nothing more.
- Account for what each of the seven types in `FreightRating.OverAbstracted` adds, and name the ordering invariant the tower leaves unstated.
- Prove two shapes of one calculation equivalent by running both against the same inputs.
- Name the condition that makes `IRateComponent` and `IRateComponentFactory` pay: a second rating scheme.
- Recognize a Strategy-and-Factory tower around a calculation with one implementation as misuse, and ask the question that exposes it: what is the second thing this interface is for?

## Why this matters

A freight rate quote for one consignment is three numbers added together: a percentage of the declared value, a charge per kilogram, and a fuel levy on the sum of the two when the levy applies. An engineer, or an AI assistant given the same request, can write it as a static method that takes a record and returns a number. Or they can write a small platform: an `IRateComponent` interface, three classes that implement it, an `IRateComponentFactory`, one factory, and an engine that folds the components over a running subtotal.

Both compile, both pass their tests, and both return the same quote for every request tried. Only one fits a rater that has one rating scheme. Strategy and Factory Method pay when a behavior varies by call site or a construction decision has to move away from the code that uses it. This rater has never had a second component list or a second order, so the interfaces carry variation that doesn't exist.

The cost shows up when someone reads it. Seven types stand between "how is this quote computed" and an answer `RateCalculator.Quote` gives in three lines. Finding the fuel levy means tracing a factory call to the list it builds, then noticing that `FuelLevyComponent` is correct only if it runs after the two components it taxes. Nothing in `IRateComponent` says so.

{% include excalidraw.html name="40-anti-patterns-and-pattern-misuse" caption="Figure 40.1: Two paths to the same freight quote: a Strategy-and-Factory component tower on one side, a single static method on the other, both fed the same RateRequest." %}

## The right-sized form: a quote is three lines of arithmetic

Both forms rate the same request record:

```csharp
public sealed record RateRequest(decimal DeclaredValue, decimal WeightKilograms, bool FuelLevyApplies);
```

The right-sized form:

```csharp
public static class RateCalculator
{
    public static decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseRate = request.DeclaredValue * 0.015m;
        var weight = request.WeightKilograms * 2.50m;
        var levy = request.FuelLevyApplies ? (baseRate + weight) * 0.08m : 0m;

        return baseRate + weight + levy;
    }
}
```

`Quote` is `static` because there's no state between calls and no collaborator to inject. The three locals name the three numbers, and the ternary is the only conditional: the levy is 8 percent of base rate plus weight charge, applied only when `FuelLevyApplies` is `true`. Base rate, weight and levy are three steps in one formula, not three implementations of one contract, so nothing hides behind an interface.

The formula's behavior:

| Request | Quote |
| --- | --- |
| `1_000m` declared, `100m` kg, no levy | `265m` (`15m` base plus `250m` weight) |
| Same, levy applies | `286.20m` (8 percent of `265m` added) |
| Every field at its default | `0m` |
| Same request twice | Same result (no hidden state) |

`RateCalculator` exposes exactly one public static method, and a reflection check guards that shape, so a second public entry point fails before review.

## The over-abstracted tower: seven types for one calculation

`FreightRating.OverAbstracted` computes the same quote through Strategy and Factory Method built the usual way: a shared interface, several implementations, a factory that assembles them, and an engine that drives them without knowing their concrete types.

```csharp
public interface IRateComponent
{
    decimal Apply(RateRequest request, decimal runningSubtotal);
}
```

Each component sees the request and the subtotal so far, and returns its contribution. `BaseRateComponent` and `WeightSurchargeComponent` ignore `runningSubtotal`:

```csharp
public sealed class BaseRateComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) => request.DeclaredValue * 0.015m;
}
```

`FuelLevyComponent` is the one whose correctness depends on its position:

```csharp
public sealed class FuelLevyComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) =>
        request.FuelLevyApplies ? runningSubtotal * 0.08m : 0m;
}
```

It produces the right number only after the other two have added to the subtotal. `IRateComponent` states nothing about order, so the guarantee lives in whichever factory builds the list. The collapsed method states the same invariant once, in the order its locals are declared.

The factory is a seam for a second rating scheme that hasn't arrived:

{% raw %}
```csharp
public sealed class StandardRateComponentFactory : IRateComponentFactory
{
    public IReadOnlyList<IRateComponent> CreateComponents() =>
        [new BaseRateComponent(), new WeightSurchargeComponent(), new FuelLevyComponent()];
}
```
{% endraw %}

`IRateComponentFactory` declares the one method, `CreateComponents()`, and `StandardRateComponentFactory` is its only implementation. `RateEngine` takes the factory through a primary constructor and folds the list:

```csharp
public sealed class RateEngine(IRateComponentFactory factory)
{
    public decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = 0m;
        foreach (var component in factory.CreateComponents())
        {
            subtotal += component.Apply(request, subtotal);
        }

        return subtotal;
    }
}
```

Count the types: an interface for components, three components, an interface for the factory, one factory, and an engine. Seven declarations across six files say what one static method says. The tower returns the same `265m` and `286.20m`, and the factory's order, `BaseRateComponent`, then `WeightSurchargeComponent`, then `FuelLevyComponent`, is pinned by a check of its own, because order is the fact `FuelLevyComponent` depends on.

## Proving equivalence: two shapes, one quote

Two forms that each match their own hand-picked expected values can still diverge at an input nobody picked. One theory runs both against the same request and compares them:

{% raw %}
```csharp
[Theory]
[MemberData(nameof(Requests))]
public void Both_forms_produce_the_same_quote(RateRequest request)
{
    var collapsed = RateCalculator.Quote(request);
    var tower = new RateEngine(new StandardRateComponentFactory()).Quote(request);

    Assert.Equal(collapsed, tower);
}
```
{% endraw %}

The seven requests cover an all-zero request with the levy off and on, the `1_000m` request off and on, a small decimal request (`42.75m` declared, `3.5m` kg), and a near-maximum request (`999_999.99m` declared, `50_000m` kg) off and on. All seven agree. The tower and the method are one calculation computed two ways, and the rater ships the simpler one.

## Right-sizing the abstraction

{% include excalidraw.html name="40-right-sizing-the-abstraction" caption="Figure 40.2: Removing the Strategy and Factory machinery folds seven cooperating types into the three-line RateCalculator.Quote method, since no second rating scheme has ever existed to justify them." %}

The choice has nothing to do with language version. Both forms use primary constructors and collection expressions and read as current C#. The difference is abstraction level against the number of things that vary. `RateCalculator.Quote` states one rating scheme because the rater has one. `RateEngine` states a scheme assembled from swappable components because that's what Strategy and Factory Method always state, whether or not a second scheme uses the seam.

A second scheme flips the verdict:

- a contract carrier that prices by a flat per-shipment fee instead of a declared-value percentage;
- a regional lane that adds a customs-handling component the standard lane never charges.

Either gives `IRateComponentFactory` a second implementation and gives `RateEngine` a reason to accept a factory instead of a hardcoded one. Then both schemes share one engine and one set of checks for the running-subtotal fold, and neither knows the other exists.

Until then, the tower costs seven files, a factory call to trace and an unstated ordering invariant, for a quote three lines already compute. Collapsing `RateEngine` into `RateCalculator.Quote` removes a seam nothing uses, and the equivalence theory shows the quote loses nothing.

## Build, run, observe

```sh
cd examples/40-anti-patterns-and-pattern-misuse
dotnet test
```

All 16 tests pass: nine on the two forms individually, including the reflection check on `RateCalculator`'s shape and the factory's component order, and seven equivalence cases. `scripts/verify.sh` runs the same project with every other example.

## When not to use it

{% include excalidraw.html name="40-pattern-shaped-llm-output" caption="Figure 40.3: Asked for a freight-rate calculator, an AI assistant drafts the full Strategy and Factory tower. A review asks what the second rating scheme is, finds none, and collapses it to RateCalculator.Quote." %}

Strategy and Factory Method pay when the thing they abstract has more than one shape in the system: a second rating scheme, a second way to combine components, or a construction choice a caller varies at runtime. `FreightRating.OverAbstracted` has one factory, one set of three components and one fixed order. Each seam has one occupant, which is no seam at all, except that it still has to be read, tested and maintained.

The mistake runs both ways:

| Mistake | What it looks like | Cost |
| --- | --- | --- |
| Under-abstracting | One scheme hardcoded in a constructor or `switch` while a second carrier contract is already committed | The second scheme forces changes in code that has nothing to do with rating |
| Over-abstracting | `IRateComponent` and `IRateComponentFactory` before any second scheme exists | More types and indirection, against a change that may never come |

"Keeping it simple" and "building it right from the start" are both labels. What decides is whether a second occurrence exists, or is planned firmly enough to act on now. For `FreightRating` there's one scheme and none planned, so `RateCalculator.Quote` is the shape to reach for, and `RateEngine` is what a second scheme would plug into.

> **🤖 Explore with your agent**
>
> "There's one rating scheme. Write `RateCalculator.Quote(RateRequest request)` as a single public static method: `DeclaredValue * 0.015m`, plus `WeightKilograms * 2.50m`, plus 8% of that subtotal when `FuelLevyApplies`. Don't add `IRateComponent`, `IRateComponentFactory` or a `RateEngine`, and don't add a parameter for a second scheme. Before deleting `OverAbstracted.RateEngine`, add a theory that runs both forms on the same `RateRequest` values and asserts equal quotes."
>
> Run `dotnet test --project FreightRating.Tests --filter "RateCalculatorTests|EquivalenceTests"`; `Collapsed_calculator_exposes_a_single_public_quote_method` fails if the reply adds a public member.

## For deeper coverage

- Microsoft Learn, [Interface Design](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/interface-design): the Framework Design Guidelines' own case for when an interface fits a design and when a concrete type says the same thing with less machinery, the authority behind the read on `IRateComponent` and `IRateComponentFactory`.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Strategy and Abstract Factory, the two patterns applied past the point where either pays for itself in the freight rater.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
