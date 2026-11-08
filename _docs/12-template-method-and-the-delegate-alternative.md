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

- State Template Method's intent: fix the order of an algorithm's steps in one place and let each step's content vary.
- Write the inheritance form: a protected constructor, a non-virtual public template method and protected abstract steps.
- Write the delegate alternative: a record of `Func<>` values run in the same fixed order by a static method.
- Pass a country's rules as a value instead of writing a subclass, and say what that costs and buys.
- Say why a record with an interface-typed collection member can't rely on generated equality, and compare the fields that matter instead.

## Why this matters

A customs declaration always takes three moves: collect the fields the destination's authority requires, calculate the duty, and validate the shipment against that country's rules. The content changes by country; the order doesn't.

| Country | Required fields | Duty | Flag when |
| --- | --- | --- | --- |
| United States | Importer of Record, HTS code, country of origin | 2.5% of declared value, USD | Declared value over 2500.00 USD (formal entry) |
| Germany | EORI number, commodity code, Incoterms | 19% of declared value, EUR | Weight over 1000 kg (licensed broker) |
| Japan | Importer tax ID, HS code, certificate of origin | 5% of declared value plus 2000 JPY handling | Declared value over 200000 JPY (inspection) |

All three round duty with `Math.Round(amount, 2, MidpointRounding.AwayFromZero)` and flag a shipment with zero line items.

If each country's handler writes the whole sequence, a rushed new one calculates duty before it knows the required fields, or skips validation. A reviewer checks the formula and the threshold, not whether `Validate` ran after `CalculateDuty`. Taking the sequence out of the country's hands leaves three seams to write and review.

{% include excalidraw.html name="12-template-method-and-the-delegate-alternative" caption="Figure 12.1: The same three-step customs skeleton, collect fields, calculate duty, validate, built two ways: a sealed inheritance hierarchy and a record of delegates, producing identical results per country." %}

## Pattern: Template Method

### The problem

Several countries share one declaration process and differ in what each step does. The order must hold for every country, including ones not written yet, and a new country should add only its own rules.

### The solution

A **Template Method** defines an algorithm's skeleton in one method and defers the content of individual steps to code supplied separately, so the order can't change while the steps do.

### How C# models it

The inheritance form is an abstract class with one public member and three protected abstract steps:

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

The constructor is `protected` because only a subclass may construct an abstract type; CA1012 flags a public one. `UnitedStatesDeclarationBuilder`, `GermanyDeclarationBuilder` and `JapanDeclarationBuilder` are `sealed` and override only the three steps. They can't touch `Build`, because it isn't `virtual`: nothing on it can be overridden, reordered or skipped. That missing `virtual` makes this a template method rather than three hooks called from one place. Reading a subclass tells a reviewer everything the country does differently and nothing about order, because order was never its to control.

{% include excalidraw.html name="12-template-method-step-order" caption="Figure 12.2: Three countries plug different required fields, duty formulas, and validation thresholds into the same fixed three-step slot order; the order never moves, only the content." %}

### The delegate form: steps as values

The same skeleton needs no hierarchy. A record holds one function per step:

```csharp
public sealed record DeclarationSteps(
    Func<Shipment, IReadOnlyList<string>> CollectRequiredFields,
    Func<Shipment, Money> CalculateDuty,
    Func<Shipment, IReadOnlyList<string>> Validate);
```

A static method runs them in the same order:

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

Order is fixed the way any method fixes it: the calls appear in sequence, and `DeclarationSteps` gives a caller no way to run them differently. A country's rules are a value. `CountrySteps` exposes `UnitedStates`, `Germany` and `Japan`, each a `DeclarationSteps` with the same thresholds, rounding and message text as the matching subclass. A fourth country is one more static property instead of one more class. The substance is the same size, three lambdas against three overrides, but the type system no longer mints a class for what is a configuration value.

Both forms run `CollectRequiredFields`, `CalculateDuty`, `Validate` in that order, observed by recording each step as it runs, and both produce the same declaration for each of the three countries.

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

### Where inheritance still fits

The delegate form wins here because every step is a pure function of its `Shipment`. Change any of these conditions and a class costs less:

| Need | Base class | Record of delegates |
| --- | --- | --- |
| A shared helper, such as rounding or a tariff-table lookup | A protected method every subclass inherits | A static method or captured local wired into every lambda by hand |
| State carried across steps, such as a running total | Instance fields on the subclass | Threaded through parameters, or a mutable capture shared across lambdas |
| A type something else matches on, such as `is IEuCustomsDeclaration` or a `Type`-keyed registry | The subclass is the type | No identity beyond three delegate references, compared by reference |
| A step that calls shared base behavior, such as a `FormatMessage` helper inside `Validate` | A `protected` or `private` method on the base | Passed in as yet another delegate |

The last row is the case Gamma, Helm, Johnson and Vlissides lean on: template methods that call concrete, non-overridable operations on the base class, so an override can reach back into shared behavior. Both forms are idiomatic C#; the choice is which one costs less for the variation in hand.

### Comparing the two forms: record equality

Proving the forms agree looks like one equality check on two `CustomsDeclaration` records. It isn't. `RequiredFields` and `ValidationMessages` are declared `IReadOnlyList<string>`. Generated record equality compares each member with `EqualityComparer<T>.Default` for its declared type, and for an interface with no equality of its own that falls back to reference equality on the backing array. Two declarations built independently each hold their own `string[]` with the same strings in the same order, and the records compare unequal.

Compare the fields instead: `DestinationCountry`, `RequiredFields`, `Duty` and `ValidationMessages`, for each country. xUnit's `Assert.Equal` differs from the record's `Equals` here: when both arguments are `IEnumerable<T>`, it walks the sequences element by element. `Assert.Equal(fromBuilder.RequiredFields, fromEngine.RequiredFields)` compares content and order; `Assert.Equal(fromBuilder, fromEngine)` on the whole records doesn't reliably do so for those members. The same applies to any record that exposes an `IEnumerable<T>`-shaped member.

## When not to use it

- **One implementation.** A two-step sequence with one caller and no second implementation coming is a plain method that does both in order. The customs example earns the pattern with three countries today and a realistic fourth; a system with one hardcoded destination writes `BuildUnitedStatesDeclaration` and stops.
- **No shared skeleton.** If a country needs a fourth step the others skip, or runs duty before required fields, forcing it through the same `Build` produces a subclass or a `DeclarationSteps` that lies about following the process. Don't bolt on an optional fourth hook; that country isn't a variation of this algorithm.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a new country.** "Add Canada to the declarations" leaves open whether it's a new `CustomsDeclarationBuilder` subclass or a `DeclarationSteps` value. State the form and the rules:

> Add `CountrySteps.Canada` as a `DeclarationSteps` value: its required fields, its duty formula rounded with `MidpointRounding.AwayFromZero`, and its validation threshold, plus the zero-line-item check. Don't add a subclass unless the rules need a shared protected helper, state across steps, a type something matches on, or a step that calls another.

If the reply writes a subclass, ask which of those four it needs.
</blockquote>

## Build, run, observe

```sh
cd examples/12-template-method-and-the-delegate-alternative
dotnet test
```

All 9 tests pass, and `scripts/verify.sh` runs them with every other example. They cover step order for both forms, each country's distinct declaration, and field-by-field agreement between the forms for all three countries.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statement of Template Method, including the base-class-calls-concrete-operations variation.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): how compiler-generated record equality compares each declared member, including an interface-typed collection member.
- Microsoft Learn, [Equality comparisons](https://learn.microsoft.com/en-us/dotnet/standard/base-types/equality-comparisons): `EqualityComparer<T>.Default` falling back to reference equality for a type with no overridden equality member.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
