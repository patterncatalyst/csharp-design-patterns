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

- Read `RateCalculator.Quote` and explain why a freight rate quote, a
  percentage of declared value plus a per-kilogram weight charge plus a
  conditional fuel levy, needs nothing beyond a single static method to
  compute correctly.
- Account for what each of the seven types in
  `FreightRating.OverAbstracted` (`IRateComponent`, `BaseRateComponent`,
  `WeightSurchargeComponent`, `FuelLevyComponent`, `IRateComponentFactory`,
  `StandardRateComponentFactory`, `RateEngine`) adds beyond what
  `RateCalculator.Quote` already says, and name the one invariant,
  `FuelLevyComponent`'s dependence on running after the components that
  build its subtotal, that the tower leaves unstated in its own types.
- Explain how `EquivalenceTests.Both_forms_produce_the_same_quote` proves
  the collapsed method and the component tower return identical quotes
  across seven requests, including a zero-value request and a
  near-maximum one, rather than asserting equivalence from inspection.
- Identify the one condition under which `IRateComponent` and
  `IRateComponentFactory` would start paying for the extra types they
  introduce, a second rating scheme a factory could swap in, and
  distinguish that condition from flexibility offered with no second
  scheme in sight.
- Recognize a Strategy-and-Factory tower produced for a calculation with
  one implementation as pattern misuse rather than pattern application,
  and know which question to ask before accepting it: what is the second
  thing this interface is for?

## Why this matters

A freight rate quote for one consignment is three numbers added together:
a percentage of the declared value, a charge per kilogram of weight, and a
fuel levy applied to the sum of the first two when the levy is in effect.
Asked to compute that quote, an engineer, or an AI pairing partner working
from the same request, can reach for either of two shapes. One is a
static method that takes a request record and returns a number. The other
is a small platform: an `IRateComponent` interface, three classes that
implement it, an `IRateComponentFactory` interface, one factory that
implements that, and an engine that folds the factory's components over a
running subtotal. Both shapes compile. Both pass a full suite of tests.
Both return the same quote for the same request, a claim this chapter's
example proves directly rather than leaving a reader to take on faith.

Only one of those shapes matches what this freight rater needs today. The
chapters before this one spent real space on Strategy and Factory Method
because each gives C# a seam that pays for itself: a behavior that varies
by call site, or a construction decision that needs to move out of the
code that uses the result. `IRateComponent` and `IRateComponentFactory`
are built the same way, a clean interface and a passing test behind each
implementation, and they solve a problem this freight rater has never had.
There has never been a second component list or a second order to combine
a base rate, a weight surcharge, and a fuel levy. The interface exists so
an implementation can vary; in `FreightRating.OverAbstracted`, it never
does.

The cost of that tower shows up the moment someone tries to read it. Seven
files stand between a question, how is this freight quote computed, and
an answer `RateCalculator.Quote` states in three lines. Finding the fuel
levy's formula means tracing a factory call to the component list it
builds, then reading `FuelLevyComponent` closely enough to notice that its
correctness depends on running after the two components whose sum it
taxes, an ordering requirement nothing in `IRateComponent` states. None of
that reading buys a freight calculation this system does not already
have. The patterns are not the problem; every earlier chapter in this
book used one or more of them to real effect. The problem is applying
Strategy and Factory Method where the variation they exist to carry does
not exist, and billing the result as flexibility.

{% include excalidraw.html name="40-anti-patterns-and-pattern-misuse" caption="Figure 40.1: Two paths to the same freight quote: a Strategy-and-Factory component tower on one side, a single static method on the other, both fed the same RateRequest." %}

## The right-sized form: a quote is three lines of arithmetic

The example behind this chapter lives in
`examples/40-anti-patterns-and-pattern-misuse`. Both forms rate the same
request, a plain record that carries everything either one needs:

```csharp
public sealed record RateRequest(decimal DeclaredValue, decimal WeightKilograms, bool FuelLevyApplies);
```

`RateCalculator.Quote` is the form this freight rater ships with when
nothing beyond today's arithmetic has to be expressed:

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

`Quote` is `static` because nothing about a freight rate calculation needs
an instance: there is no state to hold between calls, no collaborator to
inject, and no reason a caller should new up a `RateCalculator` before
asking it for a number. `ArgumentNullException.ThrowIfNull` is the entire
defensive surface the method needs. The three local variables name the
three numbers the method combines, and the ternary on `levy` is the one
piece of conditional logic in the whole calculation: a fuel levy is 8
percent of the base rate and weight charge combined, applied only when
`FuelLevyApplies` is `true`. Nothing here hides behind an interface
because nothing here needs to vary independently of anything else; base
rate, weight, and levy are three steps in one formula, not three
implementations of one contract.

`RateCalculatorTests` checks that formula against concrete numbers rather
than trusting the arithmetic by inspection.
`Quote_sums_base_rate_and_weight_surcharge` rates a request with a
`DeclaredValue` of `1_000m` and a `WeightKilograms` of `100m` with no fuel
levy, and asserts the result equals `265m`, the base rate of `15m` plus
the weight charge of `250m`. `Quote_adds_the_fuel_levy_when_it_applies`
runs the identical request with `FuelLevyApplies: true` and asserts
`286.20m`, confirming the levy adds 8 percent of `265m`, not 8 percent of
one of the two components alone.
`Quote_omits_the_fuel_levy_when_it_does_not_apply` compares a request
against the same request `with { FuelLevyApplies = false }` and asserts
the levy-bearing quote is strictly greater, and
`Quote_returns_zero_for_an_empty_request` confirms a request with every
field at its default produces a quote of `0m` rather than some nonzero
floor hiding inside the formula. `Quote_is_a_pure_calculation` calls
`Quote` twice on the same request and asserts the two results are equal,
the test that would fail first if `Quote` ever picked up hidden state.
One further test,
`Collapsed_calculator_exposes_a_single_public_quote_method`, reaches for
reflection to assert that `typeof(RateCalculator).GetMethods` with
public, static, and declared-only flags returns exactly one method: a
guard on the shape of the type itself, not only on its output, so a
later change that grows a second public entry point into this calculator
fails a test before a reviewer has to catch it by reading a diff.

## The over-abstracted tower: seven types for one calculation

`FreightRating.OverAbstracted` computes the identical quote through a
Strategy-and-Factory Method combination built the way those patterns are
built everywhere else in this book: a shared interface, several
implementations, a factory that assembles them, and an engine that drives
the result without knowing which concrete types it holds. `IRateComponent`
is the shared contract:

```csharp
public interface IRateComponent
{
    decimal Apply(RateRequest request, decimal runningSubtotal);
}
```

Every component sees the request and the subtotal the components before
it already produced, and returns its own contribution.
`BaseRateComponent` and `WeightSurchargeComponent` both ignore
`runningSubtotal`, because the declared-value percentage and the
per-kilogram charge do not depend on anything computed earlier:

```csharp
public sealed class BaseRateComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) => request.DeclaredValue * 0.015m;
}
```

`FuelLevyComponent` is the one component in the tower whose correctness
depends on its position:

```csharp
public sealed class FuelLevyComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) =>
        request.FuelLevyApplies ? runningSubtotal * 0.08m : 0m;
}
```

Taxing `runningSubtotal` only produces the right number once
`BaseRateComponent` and `WeightSurchargeComponent` have already run and
added their contributions to that subtotal. `IRateComponent` states
nothing about ordering; the guarantee that `FuelLevyComponent` runs last
lives entirely in whichever factory assembles the list, exactly the kind
of implicit invariant a collapsed method states once, in the order its
local variables are declared, and a component tower has to re-establish
through assembly order instead.

`IRateComponentFactory` is the seam the tower builds for a second rating
scheme that has not shown up:

{% raw %}
```csharp
public interface IRateComponentFactory
{
    IReadOnlyList<IRateComponent> CreateComponents();
}
```
{% endraw %}

`StandardRateComponentFactory` is the only implementation this book has
ever needed:

{% raw %}
```csharp
public sealed class StandardRateComponentFactory : IRateComponentFactory
{
    public IReadOnlyList<IRateComponent> CreateComponents() =>
        [new BaseRateComponent(), new WeightSurchargeComponent(), new FuelLevyComponent()];
}
```
{% endraw %}

`RateEngine` takes an `IRateComponentFactory` through a primary
constructor and folds whatever list that factory returns over a running
subtotal:

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

Counting types rather than lines makes the comparison concrete: an
interface for the components, three classes that implement it, an
interface for the factory, one class that implements that, and an engine
that drives the fold, seven declarations across six files, against the
single static method `RateCalculator.Quote` needs to say the same thing.
`RateCalculatorTests` exercises this tower from the same file as the
collapsed form. `Rate_engine_sums_its_components` builds a `RateEngine`
over a `StandardRateComponentFactory` and asserts the same `265m` the
collapsed method returns for the same inputs, and
`Rate_engine_applies_the_fuel_levy_component_on_the_running_subtotal`
confirms `286.20m` when the levy applies, the identical two figures
`RateCalculatorTests` already checked against `RateCalculator.Quote`.
`Standard_factory_creates_the_three_rate_components_in_order` asserts
`CreateComponents` returns exactly `BaseRateComponent`, then
`WeightSurchargeComponent`, then `FuelLevyComponent`, using
`Assert.Collection` to pin both the count and the order, since the order
is the one fact `IRateComponent` leaves `FuelLevyComponent` depending on
without stating.

## Proving equivalence: two shapes, one quote

Matching assertions between two test classes is a start, not a proof.
`EquivalenceTests` closes the gap with one theory run against both forms
directly:

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

`Requests` supplies seven cases through a `TheoryData<RateRequest>`: an
all-zero request with the levy both off and on, the `1_000m`-declared-value
request `RateCalculatorTests` already checks by hand with the levy both
off and on, a small decimal request (`42.75m` declared, `3.5m`
kilograms), and a request near the top of a realistic range
(`999_999.99m` declared, `50_000m` kilograms) with the levy both off and
on. Every one of the seven runs both `RateCalculator.Quote` and
`new RateEngine(new StandardRateComponentFactory()).Quote` against the
identical `request` value and asserts the two results are equal. A test
suite that only checked each form against its own hand-picked expected
values, as `RateCalculatorTests` does on its own, could still miss a
divergence at an input neither suite's author happened to pick; running
both forms against the same seven requests inside one assertion rules
that out directly rather than hoping two independently chosen expected
values happen to agree. The tower and the collapsed method are not two
designs that merely resemble each other. They are, by this test, the same
calculation, computed two different ways, and the simpler of those two
ways is the one this freight rater ships behind `RateCalculator.Quote`.

## Right-sizing the abstraction

{% include excalidraw.html name="40-right-sizing-the-abstraction" caption="Figure 40.2: Removing the Strategy and Factory machinery folds seven cooperating types into the three-line RateCalculator.Quote method, since no second rating scheme has ever existed to justify them." %}

The question that decides whether `IRateComponent` and
`IRateComponentFactory` pay for themselves has nothing to do with which
C# release built them. Both forms in this example compile against the
same .NET 10 SDK, use the same primary constructor and collection
expression syntax, and read as current an idiom as anything else in this
book. The difference between them is not modern code against older code;
it is abstraction level against the number of things that vary.
`RateCalculator.Quote` states one rating scheme because this freight
rater has one rating scheme. `RateEngine` states a rating scheme as
something assembled from a factory-built list of swappable components
because that is what Strategy and Factory Method always state, whether or
not a second scheme or a second factory ever shows up to use the seam.

A second rating scheme is exactly what would flip that verdict. A
contract carrier that prices consignments by a flat per-shipment fee
instead of a declared-value percentage, or a regional lane that adds a
customs-handling component the standard lane never charges, gives
`IRateComponentFactory` a second implementation to return, and gives
`RateEngine` a reason to accept whichever factory a caller constructs it
with rather than a hardcoded `StandardRateComponentFactory`. At that
point the interface stops decorating a single rating scheme; it becomes
the thing that lets two rating schemes share one engine and one test
suite for the mechanics both schemes rely on, the running-subtotal fold,
without either scheme's components knowing the other exists. That is the
same shape earlier chapters in this book built Strategy and Factory
Method around, the only difference being the one fact this freight rater
has not yet had: more than one occurrence.

Until that second scheme exists, the tower's seven types buy a reader
seven files to read, a factory call to trace, and an ordering invariant
`FuelLevyComponent` depends on and `IRateComponent` never states, for a
calculation `RateCalculator.Quote` already computes correctly in three
lines. Collapsing `RateEngine` back to `RateCalculator.Quote` is not a
smaller, cheaper version of the same design. It removes a seam nothing in
the system currently uses, and the equivalence test in this chapter's
example is the proof that nothing about the freight quote itself is lost
when that seam goes.

## Build, run, observe

The example behind this chapter is
`examples/40-anti-patterns-and-pattern-misuse`, two freight-rating forms
over the same `RateRequest`, covered by one test project. It is
standalone-testable from its own directory:

```sh
cd examples/40-anti-patterns-and-pattern-misuse
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 16
  failed: 0
  succeeded: 16
  skipped: 0
  duration: 730ms
```

The sixteen cases split across two test classes.
`RateCalculatorTests` accounts for nine: five against the collapsed
`RateCalculator.Quote` (the base-rate-and-weight sum, the fuel levy
applied and omitted, a zero-value request, and purity across repeated
calls), one guarding the collapsed type's shape through reflection, and
three against the `RateEngine`/`StandardRateComponentFactory` tower
(summing its components, applying the levy on the running subtotal, and
the factory's component order). `EquivalenceTests` runs the remaining
seven, one theory case per `RateRequest` in `Requests`, each asserting
`RateCalculator.Quote` and
`new RateEngine(new StandardRateComponentFactory()).Quote` return the
identical decimal for the identical request.

## When not to use it

Strategy and Factory Method pay for themselves when the thing they
abstract over has more than one shape in the system that uses it: more
than one rating scheme behind `IRateComponentFactory`, more than one way
to combine components behind `IRateComponent`, or a construction decision
a caller needs to vary at runtime rather than fix at compile time. None
of those conditions describe `FreightRating.OverAbstracted` as it stands
in this book. One factory, one set of three components, one fixed order:
every seam the tower offers has exactly one occupant, which amounts to
having no seam at all, except that the seam still has to be read, tested,
and maintained as though a second occupant might arrive.

The failure mode runs in both directions. Under-abstracting a problem
that does have more than one real shape, hardcoding a single rating
scheme into a constructor or a `switch` statement when a second carrier
contract is already committed, forces a later change to touch code
that has nothing to do with rating just to make room for a second scheme.
Over-abstracting a problem that has exactly one shape, building
`IRateComponent` and `IRateComponentFactory` before any second scheme
exists, spends real effort in the other direction: more types to
maintain and more indirection to read through, against a change that may
never come. Under-abstracting reads as keeping things simple, and
over-abstracting reads as building it right from the start; neither
label checks the one fact that decides which approach fits here, whether
a second occurrence of this thing exists or is planned with enough
certainty to act on now. For `FreightRating`, today's answer is a single
rating scheme with no second one in sight, which is why this book ships
`RateCalculator.Quote` as the form a reader should reach for, keeping
`RateEngine` and its supporting types around as what a second scheme
would plug into, not as the default shape a one-off calculation should
take.

<blockquote class="ai-sidebar" markdown="1">
Asked to compute a freight rate quote, an AI pairing partner will
sometimes return exactly this: an `IRateComponent` interface, a class per
charge, an `IRateComponentFactory` to assemble them, and a `RateEngine`
to fold the result, for a calculation that never had more than one rating
scheme to abstract over. The tower is not wrong code. Every type
compiles, every method does what its name says, and a generated test
suite can even pass against it. What is missing is the second occupant
that would make the interface pay for itself: a second factory, or a
second component set, something the request never mentioned and the
assistant never asked about. A model trained to recognize "compute a
value from several contributing factors" as a Strategy-shaped problem
will reach for Strategy whether or not a second strategy exists, because
nothing in that pattern-matching step checks for one.

Review a generated answer like this one the way this chapter's
equivalence test does: ask what a collapsed, single-method version of the
same calculation would look like, and check whether the generated
interfaces and factories support anything beyond what that single method
already states. If the answer is one rating scheme, one component
list, one order, fold the tower back into the method it replaced, the
direction `RateCalculator.Quote` takes `FreightRating.OverAbstracted` in
this chapter, and keep the interface split in reserve for the day a
second rating scheme needs it.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Interface Design](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/interface-design): the Framework Design Guidelines' own case for when an interface fits a design and when a concrete type says the same thing with less machinery, the authority behind this chapter's read on `IRateComponent` and `IRateComponentFactory`.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Strategy and Abstract Factory, the two patterns applied past the point where either pays for itself in this chapter's example.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
