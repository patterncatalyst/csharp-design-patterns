---
title: "Template Method and the delegate alternative"
order: 12
layout: tutorial
sectionid: docs
part: "selecting-varying-behavior"
description: "Where an inheritance-based skeleton still fits, and where a delegate replaces it."
---

<!-- EXAMPLE (shipping domain): Customs declaration generation, varying by destination country. -->

## Learning objectives

By the end of this chapter you can:

- Name what Template Method fixes and what it leaves open: the order of an
  algorithm's steps is set in one place, and each step's content varies on its
  own.
- Write the inheritance form: a protected constructor, a non-virtual public
  template method, and protected abstract steps a subclass overrides without
  touching the skeleton.
- Write the delegate-record alternative: a record of `Func<>` values as a
  swappable step set, run by a static method in the same fixed order the
  inheritance form uses.
- Pass a country's rules as a value at the call site instead of writing a new
  subclass, and say what that trades away and what it buys.
- Explain why a record with an interface-typed collection member can't rely on
  compiler-generated equality for a whole-object comparison, and write an
  assertion that checks the fields that matter.

## Why this matters

Template Method fixes the order of an algorithm's steps in one place and lets
the content of each step vary on its own. Reach for it when a sequence has to
stay the same across every variation while the work inside each step changes
from one case to the next. The pattern puts the sequence where no variation can
reach it, so each variation only supplies the steps.

The value is in what that prevents. When every variation carries its own copy
of the sequence, nothing stops one of them from running the steps out of order
or dropping a step. That bug is hard to catch in review, because the reviewer
is reading the part that changes, the step content, not the part that should
never change, the order.

Customs declarations have this shape. Every international shipment goes through
the same three moves, whatever its destination. Collect the fields the
destination's customs authority requires. Calculate the duty owed. Validate the
shipment against that country's rules to flag anything that needs a human before
it crosses a border.

The content of each move changes from country to country; the order never does.
The United States wants an Importer of Record and an HTS code. Germany wants an
EORI number and a commodity code. Japan wants a tax ID and a certificate of
origin. Duty is a flat percentage in two countries and a percentage plus a
handling fee in the third. The scrutiny flag trips on a declared-value
threshold in two countries and a weight threshold in the third. Through all of
it, every country collects its fields, then calculates its duty, then validates.

Let each country carry its own copy of that sequence and sooner or later one of
them calculates duty before confirming the fields, or skips validation
entirely. Template Method moves the sequence out of reach. Each country writes
only its content for three fixed steps, and that content is all a reviewer has
to check.

The example for this chapter builds that skeleton two ways, in
`examples/12-template-method-and-the-delegate-alternative`.
`CustomsDeclarationBuilder` is the classic inheritance form: an abstract class
whose `Build` method runs the sequence and seals it against reordering, with
three protected steps a subclass fills in. `DeclarationEngine` is the
alternative: a static method that runs the identical sequence but reads each
step from a `DeclarationSteps` record of `Func<>` values instead of an
override. Both produce the same `CustomsDeclaration` for the same shipment and
country.

{% include excalidraw.html name="12-template-method-and-the-delegate-alternative" caption="Figure 12.1: The same three-step customs skeleton, collect fields, calculate duty, validate, built two ways: a sealed inheritance hierarchy and a record of delegates, producing identical results per country." %}

## The inheritance form: a sealed skeleton

The inheritance form puts the sequence in a base class and makes it unreachable
from below. That is where the pattern's one guarantee lives: no subclass, now or
later, can change the order. `CustomsDeclarationBuilder` has one public method,
`Build`, that runs the three steps in order, and three protected abstract steps
a subclass supplies:

```csharp
public abstract class CustomsDeclarationBuilder
{
    protected CustomsDeclarationBuilder() { }                 // #A

    public CustomsDeclaration Build(Shipment shipment)       // #B
    {
        ArgumentNullException.ThrowIfNull(shipment);

        var requiredFields = CollectRequiredFields(shipment); // #C
        var duty = CalculateDuty(shipment);
        var validationMessages = Validate(shipment);

        return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
    }

    protected abstract IReadOnlyList<string> CollectRequiredFields(Shipment shipment); // #D
    protected abstract Money CalculateDuty(Shipment shipment);
    protected abstract IReadOnlyList<string> Validate(Shipment shipment);
}
```

`#A` The constructor is `protected`: only a concrete subclass constructs the
builder, which the analyzer rule CA1012 enforces.
`#B` `Build` carries no `virtual` keyword, so no subclass can override, reorder,
or skip it. That missing keyword is what makes this a template method rather
than three loosely related hooks called from the same place.
`#C` The step order lives here, in one place, fixed at compile time for every
subclass that will ever exist.
`#D` The three seams each country fills.

The three per-country classes, `UnitedStatesDeclarationBuilder`,
`GermanyDeclarationBuilder`, and `JapanDeclarationBuilder`, are each `sealed` and
override only those three steps. `UnitedStatesDeclarationBuilder`, for one,
requires an Importer of Record and an HTS code, charges 2.5 percent of declared
value, and flags a shipment once its value exceeds 2500.00 USD. Germany and
Japan differ only in their fields, formulas, and thresholds, which Figure 12.2
lays out side by side. One check is shared across all three: a shipment with
zero line items is flagged everywhere, since every customs authority wants to
see at least one declared good. Reading any one subclass shows what that country
does differently. None of them says anything about step order, because the order
was never theirs to control.

{% include excalidraw.html name="12-template-method-step-order" caption="Figure 12.2: Three countries plug different required fields, duty formulas, and validation thresholds into the same fixed three-step slot order; the order never moves, only the content." %}

## The delegate form: steps as values

A class per country is a lot of ceremony when the rules are just three
functions. The delegate form keeps the same guarantee, the fixed order, but
drops the hierarchy. `DeclarationSteps` is a record holding the three step
functions, one each:

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

        var requiredFields = steps.CollectRequiredFields(shipment); // #A
        var duty = steps.CalculateDuty(shipment);
        var validationMessages = steps.Validate(shipment);

        return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
    }
}
```

`#A` The same fixed order, read from the record this time. An ordinary method
fixes order the way the non-virtual `Build` did: the calls run in the sequence
they appear in, and nothing about `DeclarationSteps` lets a caller run them
differently.

Nothing here subclasses anything. A country's rules aren't a type at all.
They're a value, an instance of `DeclarationSteps` holding three closures.

`CountrySteps` is the delegate form's counterpart to the three builder
subclasses. It exposes a `UnitedStates`, a `Germany`, and a `Japan` property,
each a `DeclarationSteps` value that reproduces the same rules as the matching
`CustomsDeclarationBuilder` subclass, down to the same thresholds, rounding,
and message text. Reading `CountrySteps.Japan` tells a reviewer the same three
facts reading `JapanDeclarationBuilder` did: which fields, which formula, which
threshold. What's gone is the type itself.

Adding a fourth country in the delegate form means adding one static property
that returns a `DeclarationSteps` value. In the inheritance form it means
adding one sealed class. The two are the same size, three lambdas against three
method overrides. The difference is that the delegate form never asks the type
system to mint a new class for what is really a configuration value.

Both forms guarantee step order, and the test suite checks each one.
`StepOrderTests.Inheritance_form_runs_steps_in_the_fixed_declared_order`
overrides the three steps in a test-only subclass to append their names to a
log, calls `Build`, and asserts the log reads `CollectRequiredFields`,
`CalculateDuty`, `Validate`.
`StepOrderTests.Delegate_form_runs_steps_in_the_same_fixed_order` does the same
with three recording `Func<>` values passed into `DeclarationEngine.Build`.

## Where inheritance still fits

A record of delegates doesn't always beat an inheritance hierarchy. The
delegate form fits here because every step is a pure function of its `Shipment`:
no step calls another, none carries state between calls, and nothing outside
`DeclarationEngine` treats a country's rules as a type. Four situations shift the
balance back toward a class.

**A helper shared across steps.** If every country's `CalculateDuty` used the
same rounding helper or tariff-table lookup, a base class hands it down as a
protected method. With `DeclarationSteps`, that helper becomes a static method or
a captured local every lambda wires up by hand, scattered across `CountrySteps`
instead of living once on the base.

**State shared between steps.** The steps here never pass anything to each other
beyond the `shipment` each receives. When step two needs a value step one
computed, instance fields on a subclass carry it naturally; a closure-based
design has to thread it through parameters or share a captured variable, the
manual coordination a field removes.

**A type something else matches on.** If code elsewhere asked whether a builder
is a European Union declaration, or kept builders in a dictionary keyed by
`Type`, inheritance gives it a type to match against:
`GermanyDeclarationBuilder is IEuCustomsDeclaration`. A `DeclarationSteps` record
has no identity beyond its three function references.

**A step that calls another step.** This is the case classic Template Method
leans on hardest: Gamma, Helm, Johnson, and Vlissides describe steps that call
concrete, non-overridable operations on the base class mid-run. A base class
holds such a helper as a `protected` method every override can call. A `Func<>`
would have to take it in as yet another delegate.

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

Proving the two forms agree sounds like one equality assertion: build a
declaration each way and assert the two records are equal. That assertion
doesn't do what it looks like it does, and the reason shows up anywhere a
record carries a collection behind an interface type.

`CustomsDeclaration` declares `RequiredFields` and `ValidationMessages` as
`IReadOnlyList<string>`. The `Equals` a record generates compares each member
with `EqualityComparer<T>.Default` for that member's declared type. For an
interface type like `IReadOnlyList<string>`, which has no equality of its own,
`EqualityComparer<IReadOnlyList<string>>.Default` falls back to reference
equality on whatever array or list backs the interface reference. Build two
declarations independently, one from `UnitedStatesDeclarationBuilder.Build` and
one from `DeclarationEngine.Build(shipment, CountrySteps.UnitedStates)`, and
each constructs its own `string[]` holding the same three strings in the same
order. The generated record equality still reports them unequal. They're two
different array instances behind the same interface type, and reference
equality never looks inside the array to compare elements.

`ParityTests.Inheritance_and_delegate_forms_agree_for_the_same_country` is
written around that fact. It never calls `Assert.Equal` on the two whole
`CustomsDeclaration` values. It compares `DestinationCountry`, `RequiredFields`,
`Duty`, and `ValidationMessages` one at a time, as a `[Theory]` over all three
countries. The distinction matters because xUnit's `Assert.Equal` isn't the
record's `Equals`. When the compile-time type of both arguments is an
`IEnumerable<T>` such as `IReadOnlyList<string>`, `Assert.Equal` walks both
sequences and compares them element by element, which is exactly what the
record's generated equality skips. So
`Assert.Equal(fromBuilder.RequiredFields, fromEngine.RequiredFields)` does
compare the three required-field strings for content and order, even though
`Assert.Equal(fromBuilder, fromEngine)` on the whole declarations would not.

So the test compares fields, not whole records. Default record equality asks
whether the members are the same objects.
`Assert.Equal` on two sequences asks whether the elements match. The parity
test only claims the two forms produce the same field values, so it uses the
check that compares values. Any record with an `IEnumerable<T>` member faces the
same choice.

## Build, run, observe

The chapter makes two claims: both forms run the steps in the same fixed order,
and both produce the same declaration for the same shipment. The suite lets you
watch those hold rather than trust the prose. Run it from the example's own
directory, no container in the loop:

```sh
cd examples/12-template-method-and-the-delegate-alternative
dotnet test
```

The repo-level `scripts/verify.sh` runs the same xUnit v3 suite. It checks three
things:

- **The order holds, both ways.** Two `StepOrderTests` cases record the steps as
  they run and assert the order is `CollectRequiredFields`, `CalculateDuty`,
  `Validate`, once through the inheritance form and once through the delegate
  form. This is the guarantee the whole pattern rests on.
- **The content varies by country.** Four `PerCountryDeclarationTests` cases
  confirm the United States, Germany, and Japan each produce their own fields,
  duty, and validation outcome, and that one shipment run through all three comes
  back as three distinct declarations.
- **The two forms agree.** Three
  `ParityTests.Inheritance_and_delegate_forms_agree_for_the_same_country` theory
  cases, one per country, check that the builder and the engine produce the same
  declaration field by field, so the two forms are interchangeable.

Observed locally on Fedora with the .NET 10 SDK:

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
real implementation of its varying steps, now or clearly coming. A one-off,
two-step sequence with one caller and no second implementation planned
doesn't need an abstract base class or a `DeclarationSteps`-shaped record. It
needs a plain method that does both things in order. The customs example has
the variation the pattern is built for: three real countries, each
with its own formula and threshold, exist today, and a fourth is a realistic
addition. A system with one destination hardcoded and no second one
planned should write `BuildUnitedStatesDeclaration` as an ordinary method and
stop there.

The second failure mode is reaching for Template Method when the steps being
split don't share a skeleton at all, only a resemblance in their parameter
lists. Say one country's process needs a fourth step the others skip, or needs
its steps in a different order because its customs authority processes duty
before confirming required fields. Forcing that country through the same fixed
`Build` sequence produces a subclass, or a `DeclarationSteps` value, that lies
about following a shared process it doesn't follow. Don't bolt an optional
fourth hook onto the template. That country isn't a variation of the same
algorithm, so it doesn't belong behind the same skeleton.

<blockquote class="ai-sidebar" markdown="1">
Put this chapter's decision rule in the prompt, not just in the review. Ask a
model to "add Canada to the customs declarations" and it will usually return a
new sealed `CustomsDeclarationBuilder` subclass, because the Template Method
examples it trained on are overwhelmingly class hierarchies. You get the form
you left unspecified. So specify it: tell the model to add a `DeclarationSteps`
value on `CountrySteps` unless the new country needs one of the four things a
base class gives, and to name which one if it reaches for a subclass anyway.
That turns the four conditions from something you catch afterward into something
the model has to justify up front.

Then check what comes back against the same four: does the rule set share a
protected helper, carry state across steps, get pattern-matched on from outside,
or have one step call another? If the answer is no to all four, a returned
subclass is a `DeclarationSteps` value in class's clothing. Ask for it as a
value, and you drop a file without dropping any behavior.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Template Method, including the base-class-calls-concrete-operations variation this chapter's "where inheritance still fits" section draws on.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the authority for how compiler-generated record equality compares each declared member, including the interface-typed collection member this chapter's parity-test section depends on.
- Microsoft Learn, [Equality comparisons](https://learn.microsoft.com/en-us/dotnet/standard/base-types/equality-comparisons): the authority for `EqualityComparer<T>.Default` falling back to reference equality for a type with no overridden equality member.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
