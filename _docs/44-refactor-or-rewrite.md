---
title: "Refactor or rewrite"
order: 44
layout: tutorial
sectionid: docs
part: "changing-inherited-code"
description: "The judgement call, with criteria rather than a reflex."
---

<!-- EXAMPLE (shipping domain): The rate engine: refactor or rewrite. -->

## Learning objectives

By the end of this chapter you can:

- Weigh a refactor-or-rewrite decision on four criteria, test coverage, change frequency, blast radius and domain stability, and say which way each leans for a given piece of code.
- Point to the shape of `LegacyRateEngine` that closes both paths: `sealed`, a non-virtual `Quote`, a hard-coded `new LegacyTariffTable()` and no `IRateEngine`.
- Define `IRateEngine` as the stable seam that keeps the choice reversible, and build both `RefactoredRateEngine` and `RewrittenRateEngine` behind it.
- Prove two implementations equivalent with record equality across a set of scenarios.
- Bind a consumer, `QuoteService`, to the contract so that swapping engines is a one-line change at the construction site.

## Why this matters

"Refactor or rewrite" usually arrives as an opinion. One camp sees a rewrite as admitting the team never understood the problem. The other sees it as the only response to code nobody trusts. The right call depends on facts about the code in front of you, not on which camp you started in.

Four facts do most of the work:

| Criterion | Question |
| --- | --- |
| Test coverage | How much current behavior is pinned down, so a replacement has something to check against? |
| Change frequency | How often does this code change, so a cleaner shape earns back its cost? |
| Blast radius | How much of the system feels a mistake here, so how much proof does a change need? |
| Domain stability | Are the rules settled, or will today's understanding be wrong again soon? |

None hands back a verdict alone. An engine with thin coverage, rare changes, a narrow blast radius and a settled domain is a different decision from the same code changing daily and feeding a dozen services.

What keeps either answer safe is that both outcomes land behind one stable contract. Without it, choosing a rewrite commits you before you know whether the new engine agrees with the old one. With it, the rewrite and the refactor live side by side against the same interface, and you prove they agree before either has to win.

{% include excalidraw.html name="44-refactor-or-rewrite" caption="Figure 44.1: The legacy engine has no stable seam to build on, but IRateEngine gives both a refactored and a rewritten replacement a shared contract, so QuoteService can swap one for the other with no change of its own." %}

## A rate engine with nothing to build on

The legacy method's shape rules out a third option, leaving it alone and building new work around it:

```csharp
public decimal Quote(RateRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    var tariff = new LegacyTariffTable();
    var subtotal = request.DeclaredValue * 0.015m;
    subtotal += request.WeightKilograms * tariff.RatePerKilogram(request.LaneCode);

    if (DateTime.Now.Hour is >= 8 and < 18)
    {
        subtotal *= 1.1m;
    }

    return subtotal;
}
```

- `new LegacyTariffTable()` fixes the per-kilogram rate to whatever its constructor seeds.
- `0.015m` is the ad-valorem rate, inline and unnamed.
- `DateTime.Now.Hour` decides the peak surcharge from the wall clock, so the branch runs one way during ten hours of the day and the other way the rest.

`LegacyRateEngine` is also `sealed`, `Quote` isn't `virtual`, and the type implements no interface. Reflection confirms all four facts without depending on the hour: `IsSealed` is true, the single constructor takes no parameters, `IsVirtual` is false, and `typeof(IRateEngine).IsAssignableFrom` the legacy type is false. It can't be subclassed, overridden or substituted through a shared contract. Whichever way the decision goes, the first move is the same: name the contract the type should have had.

## IRateEngine: the seam that makes the choice reversible

```csharp
public interface IRateEngine
{
    RateQuote Quote(RateRequest request);
}
```

`IRateEngine` names the one behavior the legacy engine already exposes, so naming it changes nothing a rate engine computes. It changes what a caller depends on. A caller bound to `IRateEngine` doesn't know whether the instance grew out of the legacy calculation step by step or started over. Once both outcomes must satisfy the same contract, choosing one stops being a one-way door, and either can replace the other later.

{% include excalidraw.html name="44-decision-criteria" caption="Figure 44.2: Test coverage, change frequency, blast radius, and domain stability each weigh toward refactoring in place or rewriting clean. The matrix is a judgement aid, not a formula that hands back a verdict." %}

With a stable seam in place, each criterion reads this way:

| Criterion | Leans toward refactor when | Leans toward rewrite when |
| --- | --- | --- |
| Test coverage | Regression tests exist to watch each small step | Tests pin the contract, so a clean sheet has something external to check against |
| Change frequency | The code rarely changes; a rewrite's cleaner shape may never pay back | The code changes weekly and earns back the new structure many times |
| Blast radius | Wide, such as customer invoices; take small, separately verifiable steps | Narrow, such as one internal report; more risk is tolerable |
| Domain stability | Rules are still being discovered; keep existing rule evaluation intact | Rules haven't moved in years; a clean-slate assumption is safe |

Thin coverage of either kind argues for writing the tests first, against the seam, before touching either side. A rate engine quoting one rule set for stable lanes with no pending change is a weak rewrite candidate however dated its internals look, because nothing is coming that would use a cleaner shape.

No criterion is a threshold on its own, and none overrides the others by default. An engine with excellent coverage, rare changes, narrow blast radius and a settled domain may still be refactored if the team has no appetite for a rewrite's upfront cost against a payoff years away. The criteria structure the conversation; they don't replace the judgment.

## Two replacements, the same contract

`RefactoredRateEngine` takes the refactor-in-place path: the legacy calculation moved behind `IRateEngine`, with collaborators injected, still built as a running subtotal:

```csharp
public sealed class RefactoredRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine
{
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = request.DeclaredValue * RateRules.DeclaredValueRate;
        subtotal += request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode);

        var at = clock.UtcNow;
        if (PeakWindow.IsPeak(at))
        {
            subtotal *= 1m + RateRules.PeakMultiplier;
        }

        return new RateQuote(request.ConsignmentId, subtotal, at);
    }
}
```

`ITariffSource` replaces `new LegacyTariffTable()` and `IClock` replaces `DateTime.Now`, so both the rate and the peak check are substitutable. `RateRules.DeclaredValueRate` and `RateRules.PeakMultiplier` name the two literals. The order of operations didn't change. A refactor keeps that promise; it doesn't ask whether a mutable subtotal is the shape anyone would choose fresh.

`RewrittenRateEngine` answers that question differently, with one consolidated expression:

```csharp
public sealed class RewrittenRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine
{
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var at = clock.UtcNow;
        var baseAmount = (request.DeclaredValue * RateRules.DeclaredValueRate)
            + (request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode));
        var total = PeakWindow.IsPeak(at) ? baseAmount * (1m + RateRules.PeakMultiplier) : baseAmount;

        return new RateQuote(request.ConsignmentId, total, at);
    }
}
```

The two components combine into `baseAmount`, and the surcharge is one conditional expression instead of `*=` inside an `if`. Both engines take the same constructor parameters, implement the same method and read `RateRules` and `PeakWindow` instead of re-deriving the rules. Neither knows the other exists. They're comparable because both were written against the same contract, so a check outside either can ask whether they agree.

## Proving equivalence instead of assuming it

A rewrite is safe only once something checks that it agrees with the existing implementation on every input that matters. One theory builds both engines from the same `StandardTariffSource` and the same `FakeClock` instant and asserts their quotes are equal.

`RateQuote` is a record, so `Assert.Equal` compares `ConsignmentId`, `Amount` and `QuotedAt` by value. The six scenarios span both sides of the peak boundary and three lanes, including a zero-value request and one large enough to exercise rounding at scale. All six produce record-equal quotes. That turns "the rewrite feels right" into a checked fact. Without it, adopting the rewrite means trusting that a ground-up rewrite kept every edge case, on the strength of having read it carefully.

{% include excalidraw.html name="44-same-contract-two-paths" caption="Figure 44.3: Two engines sit behind one IRateEngine contract and, fed the same request and clock, return record-equal quotes. QuoteService swaps between them with a one-line change at the construction site." %}

## QuoteService: the consumer that never has to choose

Equivalence matters to a caller only if the caller is bound to the contract rather than to one engine:

```csharp
public sealed class QuoteService(IRateEngine engine)
{
    public RateQuote QuoteFor(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return engine.Quote(request);
    }
}
```

`QuoteService` takes an `IRateEngine` and forwards to it. Built around either engine, over the same request and clock, it returns the same amount, and the only difference between the two setups is the constructor call that builds the engine. `QuoteFor` never changes. That one line is the whole cost of switching a running system from the refactored engine to the rewritten one, or back.

A decision made without a seam commits the codebase as soon as the new implementation starts replacing call sites. A decision made behind a stable interface stays reversible for as long as the interface holds.

The seam also supports the usual test controls: an NSubstitute `ITariffSource` drives the rate lookup in either engine without touching its source, and `PeakWindow.IsPeak` is checked directly across the boundary through `InternalsVisibleTo`, though it's `internal` and absent from `IRateEngine`'s surface. Neither needed a new interface.

## Build, run, observe

```sh
cd examples/44-refactor-or-rewrite
dotnet test
```

All 21 tests pass: the reflection check on the legacy engine; each engine's amount off peak and at peak; `QuoteService` behind either engine; a substituted tariff source in both; five peak-boundary hours; the standard tariff rate; and six equivalence scenarios asserting record-equal quotes.

## When not to use it

Neither path is the right default, and the same criteria sometimes argue against touching the code at all. Thin coverage, rare changes, a narrow blast radius and a shifting domain make a weak rewrite candidate: the payoff, a cleaner shape for future changes, never arrives without future changes, and the risk, silently dropping a rule, is hardest to catch with thin coverage. Those facts argue just as strongly against an incremental refactor when the goal is to replace behavior rather than keep it. Refactoring is for behavior worth keeping, not a slower route to a rewrite behind a suite that says nothing changed.

A seam like `IRateEngine` isn't free either. An interface, a second implementation and an equivalence theory for a calculation with one caller, two changes a decade and one internal report cost more than they return. The seam pays when:

- more than one implementation is plausible on its own terms;
- the decision has to stay reversible while evidence accumulates;
- callers need a contract instead of one concrete type for reasons beyond this decision.

Use the criteria and the seam while the question is open. Skip both when the answer is to leave the code alone.

<blockquote class="ai-sidebar" markdown="1">
An AI assistant can score code against coverage, change frequency, blast radius and domain stability as fast as it reads it, and can draft a rewrite behind a named interface faster than most engineers type the signature. Weighing four criteria consistently across a backlog of files is tedious work an assistant does well, and a drafted rewrite gives a team something concrete to react to. What the draft can't settle is whether it computes what the original did. A rewrite that reads cleanly can still drop a rounding rule, an edge case or a boundary condition. An equivalence theory closes that gap: run the draft and the existing implementation against the same scenarios and assert equal results, instead of trusting that a clean reading means a correct one. Weighing the criteria and deciding to proceed stay with you; the equivalence check runs before either answer ships.
</blockquote>

## For deeper coverage

- Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source framing for finding and using a seam, the property that makes a refactor-in-place and a clean rewrite equally safe to attempt behind the same contract.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the discipline behind the refactor-in-place path, small behavior-preserving steps rather than a wholesale replacement.
- Microsoft Learn, [Interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces): the language reference for the `IRateEngine` contract both replacement engines implement.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection): constructor injection as the idiom behind `RefactoredRateEngine`, `RewrittenRateEngine`, and `QuoteService` each receiving their collaborators rather than constructing them.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the value-equality semantics `RateQuote` relies on for the equivalence theory's `Assert.Equal` comparison.
- Microsoft Learn, [InternalsVisibleToAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute): the attribute behind the seam onto `PeakWindow.IsPeak`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
