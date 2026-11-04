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

A customs declaration for an international shipment goes through the same
three moves, whatever its destination. Figure out which fields the
destination's customs authority requires on the paperwork. Calculate the
duty owed. Run the shipment against that country's validation rules to see
whether anything needs a human's attention before it crosses a border.

What changes from country to country is the content of each move, not the
sequence. The United States wants an Importer of Record and an HTS code.
Germany wants an EORI number and a commodity code. Japan wants a tax ID and a
certificate of origin. The duty formula is a flat percentage in two countries
and a percentage plus a handling fee in the third. The rule that flags a
shipment for extra scrutiny trips on a declared-value threshold in two
countries and a weight threshold in the third. None of that touches the
order. Every country collects its fields, then calculates its duty, then
validates, every time.

Let each country reinvent that sequence from scratch and you invite a bug no
code review catches easily. A new country's handler, written in a hurry,
calculates duty before confirming which fields are required, or skips
validation because whoever wrote it didn't realize every other country runs
one. The sequence isn't the interesting part of any country's rules, and it's
the part a reviewer is least likely to re-check line by line: the variation
under review lives in the duty formula and the validation threshold, not in
whether `Validate` ran before `CalculateDuty`. Template Method takes that
sequence out of the hands of whoever writes the next country's rules. The
only thing left to write, and to review, is the country-specific content of
three well-defined seams.

The example for this chapter lives in
`examples/12-template-method-and-the-delegate-alternative`. It builds the same
fixed three-step skeleton two ways against the same shipping domain.
`CustomsDeclarationBuilder` is the classic Template Method: an abstract class
whose public method is sealed against reordering by construction, with three
protected abstract steps a subclass fills in. `DeclarationEngine` is the
alternative: a static method that runs the identical sequence but reads each
step from a `DeclarationSteps` record of `Func<>` values instead of
dispatching through an override. Both produce the same `CustomsDeclaration`
for the same shipment and country, and the test suite proves it.

{% include excalidraw.html name="12-template-method-and-the-delegate-alternative" caption="Figure 12.1: The same three-step customs skeleton, collect fields, calculate duty, validate, built two ways: a sealed inheritance hierarchy and a record of delegates, producing identical results per country." %}

## The inheritance form: a sealed skeleton

`CustomsDeclarationBuilder` is an abstract class with exactly one public
member, `Build`, and three protected abstract members that a subclass must
supply:

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
`GermanyDeclarationBuilder`, and `JapanDeclarationBuilder`, are each `sealed`
and override only those three steps. Each fills the seams with different
content.
`UnitedStatesDeclarationBuilder` requires an Importer of Record, an HTS code,
and a country of origin; charges 2.5 percent of declared value in US dollars;
and flags a shipment for formal entry paperwork once its declared value
exceeds 2500.00 USD. `GermanyDeclarationBuilder` requires an EORI number, a
commodity code, and Incoterms; charges 19 percent in euros; and flags a
shipment once its weight exceeds 1000 kilograms, on the theory that a heavy
shipment needs a licensed customs broker on file. `JapanDeclarationBuilder`
requires an importer tax ID, an HS code, and a certificate of origin; charges
5 percent plus a flat 2000 JPY handling fee; and flags a shipment once its
declared value exceeds 200000 JPY, triggering a mandatory customs inspection.
All three duty calculations round with
`Math.Round(amount, 2, MidpointRounding.AwayFromZero)`, and all three
validation steps flag any shipment with zero line items, a check that has
nothing to do with the country: every customs authority wants to see at least
one declared good. Reading any one subclass tells a reviewer everything that
country does differently. None of them can tell a reviewer anything about step
order, because step order was never theirs to control.

{% include excalidraw.html name="12-template-method-step-order" caption="Figure 12.2: Three countries plug different required fields, duty formulas, and validation thresholds into the same fixed three-step slot order; the order never moves, only the content." %}

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
delegate form fits here because every step is a pure function of the
`Shipment` it's handed. No step calls another. No step needs state that
persists across the three calls. And nothing outside `DeclarationEngine`
treats a country's rule set as a type in its own right, the way a consumer
that pattern-matches on a concrete subclass or stores a builder in a
dictionary keyed by `Type` would. Change any one of those conditions and the
balance shifts back toward a class.

A shared protected helper is the first case. If every country's
`CalculateDuty` applied the same currency-rounding helper, or the same lookup
against a shared tariff table, a base class hands every subclass that helper
as a protected method, inherited without re-wiring at each call site. A
`DeclarationSteps` value has no equivalent. The helper becomes a separate
static method or a captured local that every lambda references by hand. That
works, but it scatters the wiring across every property in `CountrySteps`
instead of centralizing it once on the base type.

State shared across steps is the second case. Here the three steps never pass
information to each other beyond what each receives fresh in `shipment`;
`CalculateDuty` doesn't need to know what `CollectRequiredFields` just
decided. A workflow where step two needs a value step one computed, a running
total, or a flag set partway through fits a class far better than a record of
independent functions. Instance fields on the subclass carry that state across
the calls naturally. A closure-based design has to thread the value through
parameters and return values by hand, or capture a mutable variable across
lambdas, which reintroduces the shared-state bug the pattern exists to avoid.

A type hierarchy that a consumer pattern-matches on is the third case. If code
elsewhere needed to ask whether something is a European Union declaration
builder and branch on the answer, or register builders in a dictionary keyed
by `Type` for a plugin-style lookup, the inheritance form gives that consumer
something to match against: `GermanyDeclarationBuilder is IEuCustomsDeclaration`,
say, if such an interface existed. A `DeclarationSteps` record carries no
identity beyond its three function references. There's nothing to pattern-match
on besides equality of the whole value, and the record's default equality
compares delegate fields by reference, not by behavior.

A step that must call another step is the fourth case, and it's the one classic
Template Method literature leans on hardest. Gamma, Helm, Johnson, and
Vlissides describe template methods that call not just the fixed hook steps but
also concrete, non-overridable operations on the base class itself, letting a
subclass's override reach back into shared base behavior mid-step. Nothing in
`CustomsDeclarationBuilder` does that today. But if `Validate` needed to call a
shared `FormatMessage` helper that every subclass inherits and none overrides,
that helper lives naturally as a `protected` or `private` method on the base
class, callable from every override without being passed around. A `Func<>` has
no inherited private helper to call into. Every closure in `CountrySteps` is
self-contained, so one that needed shared internal behavior would have to take
it in as another delegate.

Neither form is the better default. Pure, independent per-country functions with
no shared helper and no cross-step state favor the delegate form: a value is
cheaper to add and to read than a class. Steps that share state, call shared
base behavior, or sit in a type hierarchy something else depends on favor
inheritance, where a base class gives those needs a natural home.

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

The example is standalone-testable from its own directory, with no
container in the loop:

```sh
cd examples/12-template-method-and-the-delegate-alternative
dotnet test
```

It's also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Both paths run the same xUnit v3 suite: two
`StepOrderTests` cases proving both forms run `CollectRequiredFields`, then
`CalculateDuty`, then `Validate`; four `PerCountryDeclarationTests` cases
proving the United States, Germany, and Japan rules each produce their own
required fields, duty, and validation outcome, plus a fourth confirming one
shipment run through all three countries comes back with three distinct
declarations; and three
`ParityTests.Inheritance_and_delegate_forms_agree_for_the_same_country` theory
cases, one per country, proving the two forms agree field by field. Observed
locally on Fedora with the .NET 10 SDK:

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
Asked to add a new country, a generated answer often reaches for a new sealed
subclass of `CustomsDeclarationBuilder`, even when the country's rules never
call shared base behavior and nothing else pattern-matches on the type. That's
the case the delegate form handles with one new property on `CountrySteps`.
Template Method examples in training data are overwhelmingly class hierarchies,
so a model defaults to inheritance by habit. Check a generated subclass against
the four reasons inheritance fits here: does it share a protected helper with
its siblings, carry state across its own steps, get pattern-matched on by
something outside the hierarchy, or have one step call another. If the answer is
no to all four, it's a `DeclarationSteps` value in class's clothing, and
collapsing it to a value removes a file without removing behavior.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Template Method, including the base-class-calls-concrete-operations variation this chapter's "where inheritance still fits" section draws on.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the authority for how compiler-generated record equality compares each declared member, including the interface-typed collection member this chapter's parity-test section depends on.
- Microsoft Learn, [Equality comparisons](https://learn.microsoft.com/en-us/dotnet/standard/base-types/equality-comparisons): the authority for `EqualityComparer<T>.Default` falling back to reference equality for a type with no overridden equality member.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
