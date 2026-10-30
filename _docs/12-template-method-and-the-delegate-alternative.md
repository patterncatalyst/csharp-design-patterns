---
title: "Template Method and the delegate alternative"
order: 12
layout: tutorial
sectionid: docs
part: "selecting-varying-behavior"
description: "Where an inheritance-based skeleton still earns its place, and where a delegate replaces it."
---

<!-- EXAMPLE (shipping domain): Customs declaration generation, varying by destination country. -->

## Learning objectives

By the end of this chapter you can:

- State the Template Method pattern's intent precisely: fix the order of an
  algorithm's steps in one place, and let each step's content vary
  independently of that order.
- Write the inheritance form of Template Method: a protected constructor, a
  non-virtual public template method, and protected abstract steps that a
  subclass overrides without ever touching the skeleton.
- Write the delegate-record alternative: a record of `Func<>` values as a
  swappable step set, paired with a static method that runs those functions
  in the same fixed order the inheritance form uses.
- Choose a destination country's rules as a value passed at the call site
  instead of writing a new subclass, and say what that trade costs and buys
  compared to inheritance.
- Explain why a record carrying an interface-typed collection member cannot
  rely on compiler-generated equality for a whole-object comparison, and
  write an assertion that checks the fields that actually matter instead.

## Why this matters

A customs declaration for an international shipment always goes through the
same three moves, regardless of where the shipment is headed: figure out
which fields the destination's customs authority requires on the paperwork,
calculate the duty owed, and run the shipment against that country's
validation rules to see whether anything about it needs a human's attention
before it crosses a border. What changes from country to country is the
content of each move, not the sequence. The United States wants an Importer
of Record and an HTS code; Germany wants an EORI number and a commodity
code; Japan wants a tax ID and a certificate of origin. The duty formula is
a flat percentage in two of the three countries and a percentage plus a flat
handling fee in the third. The validation rule that flags a shipment for
extra scrutiny trips on a declared-value threshold in two countries and a
weight threshold in the third. None of that variation touches the order:
every country still collects its fields, then calculates its duty, then
validates, in that sequence, every time.

A system that lets each country's implementation reinvent the whole
three-step sequence from scratch is exactly the shape that invites a bug no
code review catches easily: a new country's handler written in a hurry
calculates duty before confirming which fields are even required, or skips
validation because the engineer who wrote it didn't realize every other
country runs one. The sequence isn't the interesting part of any single
country's rules, and it's also the part a reviewer is least likely to
re-check line by line, because the variation they're reviewing for lives in
the duty formula and the validation threshold, not in whether `Validate` ran
before or after `CalculateDuty`. The pattern this chapter demonstrates
exists to take that sequence out of the hands of whoever writes the next
country's rules entirely, so that the only thing left to write, and the only
thing left to review, is the country-specific content of three well-defined
seams.

The example behind this chapter lives in
`examples/12-template-method-and-the-delegate-alternative`, and it shows the
same fixed three-step skeleton built two different ways against the same
shipping domain. `CustomsDeclarationBuilder` is the classic Template Method:
an abstract class whose public method is sealed against reordering by
construction, not by convention, with three protected abstract steps a
subclass fills in. `DeclarationEngine` is the modern alternative: a static
method that runs the identical three-step sequence, but reads each step from
a `DeclarationSteps` record of `Func<>` values instead of dispatching through
an override. Both forms produce the same `CustomsDeclaration` for the same
shipment and the same country, and the chapter's test suite proves it.

{% include excalidraw.html name="12-template-method-and-the-delegate-alternative" caption="Figure 12.1: The same three-step customs skeleton, collect required fields, calculate duty, validate, built two ways. CustomsDeclarationBuilder fixes the order in a non-virtual Build method that United States, Germany, and Japan subclasses override only at the three steps; DeclarationEngine.Build runs the identical order over a DeclarationSteps record of delegates that CountrySteps supplies per country. Both paths produce the same CustomsDeclaration for a given shipment." %}

## The inheritance form: a sealed skeleton

`CustomsDeclarationBuilder` is an abstract class with exactly one public
member, `Build`, and three protected abstract members that a subclass must
supply:

```csharp
public abstract class CustomsDeclarationBuilder
{
    protected CustomsDeclarationBuilder()
    {
    }

    public CustomsDeclaration Build(Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        var requiredFields = CollectRequiredFields(shipment);
        var duty = CalculateDuty(shipment);
        var validationMessages = Validate(shipment);

        return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
    }

    protected abstract IReadOnlyList<string> CollectRequiredFields(Shipment shipment);

    protected abstract Money CalculateDuty(Shipment shipment);

    protected abstract IReadOnlyList<string> Validate(Shipment shipment);
}
```

The constructor is `protected`, not `public`, because an abstract class has
no business being constructed on its own; only a concrete subclass can ever
call it, and the analyzer rule that flags a public constructor on an
abstract type (CA1012) is the compiler-enforced version of the same point.
The three per-country classes, `UnitedStatesDeclarationBuilder`,
`GermanyDeclarationBuilder`, and `JapanDeclarationBuilder`, are each `sealed`
and override only `CollectRequiredFields`, `CalculateDuty`, and `Validate`.
None of the three touches `Build`, because none of the three can: `Build`
carries no `virtual` keyword, so there is nothing on it for a derived class
to override, reorder, or skip. That absence of `virtual` is not an
oversight. It is the mechanism that makes this a template method rather than
three loosely related protected hooks that happen to get called from the
same place: the skeleton's shape is fixed at compile time, for every
subclass that will ever exist, including ones not written yet.

Each country's subclass fills the same three seams with entirely different
content. `UnitedStatesDeclarationBuilder` requires an Importer of Record, an
HTS code, and a country of origin; charges 2.5 percent of declared value in
US dollars; and flags a shipment for formal entry paperwork once its
declared value exceeds 2500.00 USD. `GermanyDeclarationBuilder` requires an
EORI number, a commodity code, and Incoterms; charges 19 percent of declared
value in euros; and flags a shipment once its weight exceeds 1000
kilograms, on the theory that a heavy shipment needs a licensed customs
broker on file. `JapanDeclarationBuilder` requires an importer tax ID, an HS
code, and a certificate of origin; charges 5 percent of declared value plus
a flat 2000 JPY handling fee; and flags a shipment once its declared value
exceeds 200000 JPY, triggering a mandatory customs inspection. All three
duty calculations round with
`Math.Round(amount, 2, MidpointRounding.AwayFromZero)`, and all three
validation steps also flag any shipment with zero line items, a check that
has nothing to do with the country and exists in all three because every
country's customs authority wants to see at least one declared good. Reading
any one of the three subclasses tells a reviewer everything that
country does differently; none of the three can tell a reviewer anything
about step order, because step order was never theirs to control.

## The delegate form: steps as values

The second half of the example builds the identical skeleton without a
class hierarchy. `DeclarationSteps` is a record holding three functions, one
per step:

```csharp
public sealed record DeclarationSteps(
    Func<Shipment, IReadOnlyList<string>> CollectRequiredFields,
    Func<Shipment, Money> CalculateDuty,
    Func<Shipment, IReadOnlyList<string>> Validate);
```

`DeclarationEngine` is a static class with one static method, `Build`, that
runs those three functions in the same order `CustomsDeclarationBuilder.Build`
uses:

```csharp
public static class DeclarationEngine
{
    public static CustomsDeclaration Build(Shipment shipment, DeclarationSteps steps)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        ArgumentNullException.ThrowIfNull(steps);

        var requiredFields = steps.CollectRequiredFields(shipment);
        var duty = steps.CalculateDuty(shipment);
        var validationMessages = steps.Validate(shipment);

        return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
    }
}
```

Where the inheritance form fixes order by omitting `virtual`,
`DeclarationEngine.Build` fixes order the way any ordinary method fixes
order: the three calls appear in the sequence they run in, and nothing about
`DeclarationSteps` as a type gives a caller a way to run them differently.
Nothing here subclasses anything. A country's rules are not a type at all;
they are a value, an instance of `DeclarationSteps` holding three closures.
`CountrySteps` is the delegate form's counterpart to the three builder
subclasses, exposing a `UnitedStates`, a `Germany`, and a `Japan` property,
each a `DeclarationSteps` value reproducing the identical rules as the three
`CustomsDeclarationBuilder` subclasses, down to the same thresholds, the
same rounding, and the same message text. Reading `CountrySteps.Japan` tells
a reviewer the same three facts that reading `JapanDeclarationBuilder` told
them: which fields, which formula, which threshold. What's gone is the type
itself. Adding a fourth country in the delegate form means adding one more
static property that returns a `DeclarationSteps` value; adding it in the
inheritance form means adding one more sealed class. Both additions are the
same size in substance, three lambdas against three method overrides, but
the delegate form never asks the type system to mint a new class for data
that is, in every sense that matters here, a configuration value rather than
a distinct kind of object.

The step order itself is the one property both forms guarantee identically,
and the test suite proves that guarantee for each form on its own terms
rather than taking it on faith. `StepOrderTests.Inheritance_form_runs_steps_in_the_fixed_declared_order`
builds a test-only subclass that overrides the three steps to append their
names to a shared log, calls `Build`, and asserts the log reads
`CollectRequiredFields`, `CalculateDuty`, `Validate`.
`StepOrderTests.Delegate_form_runs_steps_in_the_same_fixed_order` does the
same thing with three recording `Func<>` values passed straight into
`DeclarationEngine.Build`, and asserts the identical sequence comes back.
Neither test inspects source code or relies on a reader trusting that
`Build` wasn't edited; each one calls the method and watches what actually
ran, which is the same standard this book has applied to every other
invariant worth guaranteeing.

## Where inheritance still earns its place

Replacing an inheritance hierarchy with a record of delegates is not a
universal upgrade, and this example is deliberately built so that the
trade is visible rather than asserted. The delegate form wins cleanly here
because every step is a pure function of the `Shipment` it's handed: no
step needs to call another step, no step needs state that persists across
the three calls, and nothing outside `DeclarationEngine` ever needs to treat
a country's rule set as a type in its own right, the way a consumer that
pattern-matches on a concrete subclass or stores a builder in a dictionary
keyed by its `Type` would. Change any one of those three conditions and the
balance shifts back toward a class.

A shared protected helper is the first case. If every country's
`CalculateDuty` needed to apply the same currency-rounding helper, or the
same lookup against a shared tariff table, a base class gives every
subclass that helper for free as a protected method, inherited without
being re-wired at every call site. A `DeclarationSteps` value has no
equivalent: the helper has to be a separate static method or a captured
local that every lambda references by hand, which works but scatters the
wiring across every property in `CountrySteps` instead of centralizing it
once on the base type.

State shared across steps is the second case. This example's three steps
never need to pass information to each other beyond what each receives
fresh in `shipment`; `CalculateDuty` doesn't need to know what
`CollectRequiredFields` just decided. A workflow where step two genuinely
needs a value step one computed, a running total, a flag set partway
through, fits a class far better than a record of independent functions,
because instance fields on the subclass carry that state naturally across
the calls, while a closure-based design has to thread the value through
return values and parameters by hand or capture a mutable variable across
lambdas, which reintroduces by convention the same shared-state bug surface
the pattern exists to avoid.

A genuine type hierarchy a consumer pattern-matches on is the third case.
If code elsewhere in the system needed to ask "is this a European Union
declaration builder" and branch on the answer, or needed to register
builders in a dictionary keyed by `Type` for a plugin-style lookup, the
inheritance form gives that consumer something to match against:
`GermanyDeclarationBuilder is IEuCustomsDeclaration`, for instance, if such
an interface existed. A `DeclarationSteps` record carries no identity beyond
its three function references; there is nothing to pattern-match on besides
equality of the whole value, which the record's default equality handles
member by member for delegate fields by reference, not by behavior.

A step that must call another step is the fourth case, and it is the one
classic Template Method literature leans on hardest: Gamma, Helm, Johnson,
and Vlissides describe template methods that call not just the fixed hook
steps but also concrete, non-overridable operations defined on the base
class itself, letting a subclass's override reach back into shared base
behavior mid-step. Nothing in `CustomsDeclarationBuilder` does that today,
but if `Validate` needed to call a shared `FormatMessage` helper that every
subclass inherits and none overrides, that helper lives naturally as a
`protected` or even `private` method on the base class, callable from
every subclass's override without being passed around as a parameter. A
`Func<>` has no equivalent "inherited private helper" to call into; every
closure in `CountrySteps` is self-contained, which is exactly why they read
so uniformly short and exactly why a closure that needed shared internal
behavior would need that behavior passed in explicitly as yet another
delegate.

None of this makes one form of Template Method categorically better than
the other. Both are modern, idiomatic C#, and the honest comparison this
chapter draws is which one costs less for a specific shape of variation,
not which one is the newer or more fashionable choice. Pure, independent,
per-country functions with no shared helper, no cross-step state, and no
external code that needs to treat a rule set as a type favor the delegate
form, because a value is cheaper to add than a class and cheaper to read
when there's nothing else to say about it beyond its three functions. Steps
that share state, call shared base behavior, or participate in a type
hierarchy something else in the codebase depends on favor the inheritance
form, because a base class gives those needs a natural home that a record
of functions has to simulate by hand.

{% include codetabs.html langs="Inheritance|Delegate" %}
```csharp
// Inheritance: a country's rules are a new sealed type.
public sealed class JapanDeclarationBuilder : CustomsDeclarationBuilder
{
    protected override IReadOnlyList<string> CollectRequiredFields(Shipment shipment) =>
        ["Importer Tax ID", "HS Code", "Certificate of Origin"];

    protected override Money CalculateDuty(Shipment shipment) =>
        new(Math.Round(shipment.DeclaredValue * 0.05m, 2, MidpointRounding.AwayFromZero) + 2000m, "JPY");

    protected override IReadOnlyList<string> Validate(Shipment shipment) =>
        shipment.DeclaredValue > 200000m
            ? ["Import declaration requires customs inspection over 200000 JPY"]
            : [];
}

var declaration = new JapanDeclarationBuilder().Build(shipment);
```
```csharp
// Delegate: a country's rules are a value.
public static DeclarationSteps Japan { get; } = new(
    CollectRequiredFields: _ => ["Importer Tax ID", "HS Code", "Certificate of Origin"],
    CalculateDuty: shipment =>
        new Money(Math.Round(shipment.DeclaredValue * 0.05m, 2, MidpointRounding.AwayFromZero) + 2000m, "JPY"),
    Validate: shipment =>
        shipment.DeclaredValue > 200000m
            ? ["Import declaration requires customs inspection over 200000 JPY"]
            : []);

var declaration = DeclarationEngine.Build(shipment, CountrySteps.Japan);
```

## Record equality and the parity test

Proving the two forms agree sounds like it should be a single equality
assertion: build a declaration both ways and assert the two records are
equal. That assertion does not do what it looks like it does here, and the
reason is worth naming precisely because it recurs anywhere a record
carries a collection behind an interface type. `CustomsDeclaration` declares
`RequiredFields` and `ValidationMessages` as `IReadOnlyList<string>`. The
compiler-generated `Equals` a record gets for free compares each member
using `EqualityComparer<T>.Default` for that member's declared type, and for
an interface type like `IReadOnlyList<string>` with no overridden equality
of its own, `EqualityComparer<IReadOnlyList<string>>.Default` falls back to
reference equality on whatever concrete array or list backs the interface
reference. Two declarations built independently, one by
`UnitedStatesDeclarationBuilder.Build` and one by
`DeclarationEngine.Build(shipment, CountrySteps.UnitedStates)`, each
construct their own `string[]` with the same three strings in it, same
order, same content, and the generated record equality would still report
them unequal, because they are two different array instances sitting
behind the same interface type, and reference equality on an interface
member never looks inside the array to compare its elements.

`ParityTests.Inheritance_and_delegate_forms_agree_for_the_same_country` is
written around that fact rather than against it. It never calls
`Assert.Equal` on the two whole `CustomsDeclaration` values; it compares
`DestinationCountry`, `RequiredFields`, `Duty`, and `ValidationMessages`
individually, as a `[Theory]` running over all three countries. That
distinction matters because xUnit's `Assert.Equal` is not the same equality
check as the record's own `Equals`: when the compile-time type of the two
arguments is an `IEnumerable<T>` such as `IReadOnlyList<string>`,
`Assert.Equal` walks both sequences and compares them element by element,
which is exactly the element-wise comparison the record's own generated
equality skips. Asserting `Assert.Equal(fromBuilder.RequiredFields,
fromEngine.RequiredFields)` therefore does compare the three required-field
strings for content, in order, even though `Assert.Equal(fromBuilder,
fromEngine)` on the two declarations as a whole would not reliably do the
same for those same fields. The fix is not a smarter assertion on the whole
record; it's recognizing that a record's own default equality and a
collection-aware assertion library answer different questions, and picking
the one whose comparison actually matches the claim the test makes: that
the two forms, built independently, produce declarations whose fields agree
value for value, not necessarily the same object graph underneath. The same
distinction between a record's default equality and element-wise
comparison for collection-backed members applies to any record exposing an
`IEnumerable<T>`-shaped member, not just this one.

## Build, run, observe

The example is standalone-testable from its own directory, with no
container in the loop:

```sh
cd examples/12-template-method-and-the-delegate-alternative
dotnet test
```

It's also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Both paths run the same xUnit v3 suite: two
`StepOrderTests` cases proving both forms run `CollectRequiredFields`, then
`CalculateDuty`, then `Validate`, in that order; four
`PerCountryDeclarationTests` cases proving the United States, Germany, and
Japan rules each produce their own distinct required fields, duty, and
validation outcome, plus a fourth case confirming one shipment run through
all three countries comes back with three distinct declarations; and three
`ParityTests.Inheritance_and_delegate_forms_agree_for_the_same_country`
theory cases, one per country, proving the inheritance form and the
delegate form agree field by field. Observed locally on Fedora with the
.NET 10 SDK:

```
Test run summary: Passed!
  total: 9
  failed: 0
  succeeded: 9
  skipped: 0
  duration: 1s 210ms
```

## When not to use it

Template Method, in either form, is built for a skeleton with more than one
real implementation of its varying steps, now or clearly coming. A
one-off, two-step sequence with exactly one caller and no second
implementation in sight does not need an abstract base class or a
`DeclarationSteps`-shaped record; it needs a plain method that does both
things in order, because a skeleton built for variation that never arrives
is an abstraction with a carrying cost and no buyer. The customs example in
this chapter earns the pattern because three real countries, each with a
genuinely different formula and threshold, exist today and a fourth is a
realistic addition; a system with one destination country hardcoded and no
second one planned should write `BuildUnitedStatesDeclaration` as an
ordinary method and stop there.

The second failure mode is reaching for Template Method when the "steps"
being split don't actually share a skeleton at all, only a superficial
resemblance in their parameter lists. If one country's declaration process
genuinely needs a fourth step the others skip entirely, or needs its three
steps in a different order because its customs authority processes duty
before confirming required fields, forcing that country through the same
fixed `Build` sequence as the other two produces a subclass, or a
`DeclarationSteps` value, that lies about following a shared process it
doesn't actually follow. The honest fix at that point is not a four-step
template with an optional fourth hook bolted on; it's recognizing that the
country in question isn't a variation of the same algorithm and doesn't
belong behind the same skeleton.

> **AI sidebar.** Asked to add a new country to this example, an AI pairing
> partner will sometimes reach straight for a new sealed subclass of
> `CustomsDeclarationBuilder` even when the actual task is "add one country
> whose rules never need to call shared base behavior and that nothing else
> in the codebase pattern-matches on," the exact shape the delegate form
> handles with a single new property on `CountrySteps` instead of a new
> file. The instinct comes from training data where Template Method
> examples are overwhelmingly shown as class hierarchies, so a model
> defaults to inheritance by habit rather than by evaluating whether this
> particular variation needs anything a class gives that a value doesn't.
> Check a generated subclass against the four reasons inheritance earns its
> place in this chapter: does it share a protected helper with its
> siblings, does it carry state across its own steps, does something
> outside the hierarchy pattern-match on its type, and does one step call
> another. A generated country handler that answers no to all four is a
> `DeclarationSteps` value wearing a class's clothes, and collapsing it back
> to a value removes a file without removing any behavior.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Template Method, including the base-class-calls-concrete-operations variation this chapter's "where inheritance still earns its place" section draws on.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the authority for how compiler-generated record equality compares each declared member, including the interface-typed collection member this chapter's parity-test section depends on.
- Microsoft Learn, [Equality comparisons](https://learn.microsoft.com/en-us/dotnet/standard/base-types/equality-comparisons): the authority for `EqualityComparer<T>.Default` falling back to reference equality for a type with no overridden equality member.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
