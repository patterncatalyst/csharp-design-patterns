---
title: "Builder, required members, and Prototype with with"
order: 16
layout: tutorial
sectionid: docs
part: "creating-objects-under-control"
description: "Why required members and collection expressions often beat a Builder, and Prototype as a one-line with expression."
---

<!-- EXAMPLE (shipping domain): A booking request with many optional fields; amendments via with. -->

## Learning objectives

By the end of this chapter you can:

- Use `required` members and object initializers so a `BookingRequest` can't be built without its mandatory fields.
- Tell a compile-time failure (CS9035) from the runtime failure a fluent builder reports from `Build()`.
- Write a validating `BookingRequestBuilder` for rules an initializer can't express, and for staged assembly.
- Implement Prototype as a `with` expression against a template that cloning never mutates.
- Apply the shallow-copy rule `with` follows, and give a clone its own list with a collection-expression spread.

## Why this matters

A booking request needs three facts before a carrier can quote it: origin, destination and declared value. The other five fields have sensible defaults: service level `"standard"`, no extra line items, no insurance, no instructions, no customer reference.

The GoF-era answer to a type with a few mandatory fields and many optional ones is Builder: a separate object accumulates state and produces the immutable result once, in `Build()`. That made sense when a language couldn't mark a field mandatory on the type. An eight-parameter constructor is unreadable at the call site, and a public setter on every field leaves the type mutable forever.

The `required` modifier marks a property mandatory where it's declared, and the compiler rejects an initializer that skips it. A collection-expression default, `LineItems = []`, gives an optional list an empty start. Together they build most such types with one initializer and no builder. A builder is still the right tool for a rule that spans more than presence, or for a request assembled across several lines of logic. Prototype, deriving a new request from an existing one, becomes a `with` expression instead of a hand-written `Clone`.

{% include excalidraw.html name="16-builder-required-members-prototype-with" caption="Figure 16.1: Three ways to produce a BookingRequest: a required-member object initializer, a validating BookingRequestBuilder that defers checks to Build(), and a with expression that clones and amends a reusable template." %}

## Pattern: Builder

### The problem

A caller constructs a `BookingRequest` and forgets the declared value. With a settable class, nothing notices until a quote fails downstream. A request can also be present but wrong: a destination that is an empty string, or a declared value that is negative. And some requests are assembled piece by piece: the origin comes from a warehouse lookup, line items come from a loop over a surcharge schedule, and the service level is known only after several branches.

### The solution

A **Builder** separates the step-by-step construction of a complex object from its representation, so the result exists only once every rule has been checked.

| Need | Tool |
| --- | --- |
| Mandatory fields present | `required` members, checked at compile time |
| Optional fields with defaults | Initializers at the property, such as `= "standard"` and `= []` |
| A rule beyond presence, such as non-blank or non-negative | A builder's `Build()` |
| Assembly spread across several statements or branches | A builder |

### How C# models it

`Money` is a `readonly record struct` whose primary constructor rejects a negative amount or a malformed currency code, with `Money.Of(amount, currency = "USD")` as the call-site form. `LineItem` is an immutable record. `BookingRequest` declares the mandatory and optional split itself:

```csharp
public sealed record BookingRequest
{
    public required string Origin { get; init; }

    public required string Destination { get; init; }

    public required Money DeclaredValue { get; init; }

    public string ServiceLevel { get; init; } = "standard";

    public IReadOnlyList<LineItem> LineItems { get; init; } = [];

    public bool InsuranceRequested { get; init; }

    public string? SpecialInstructions { get; init; }

    public string? CustomerReference { get; init; }
}
```

An initializer that sets the three required members is a complete request:

{% raw %}
```csharp
var request = new BookingRequest
{
    Origin = "Chicago, IL",
    Destination = "Denver, CO",
    DeclaredValue = Money.Of(500.00m),
};
```
{% endraw %}

The five optional members come back at their declared defaults: `"standard"`, an empty list, `false` and two `null`s. Omit `DeclaredValue` and the build fails with CS9035, "Required member 'BookingRequest.DeclaredValue' must be set in the object initializer or attribute constructor." The malformed request never runs. For a type shaped like this, the initializer removes the reason a Builder exists.

### Where a Builder still fits

`required` checks presence. It can't reject a blank destination, because `string` has no notion of non-blank, or a negative value. `BookingRequestBuilder` is a fluent, mutable accumulator that defers every check to the one method that produces a request:

```csharp
public BookingRequest Build()
{
    if (string.IsNullOrWhiteSpace(_origin))
    {
        throw new BookingValidationException("A booking request needs a non-empty origin.");
    }

    if (string.IsNullOrWhiteSpace(_destination))
    {
        throw new BookingValidationException("A booking request needs a non-empty destination.");
    }

    if (_declaredValue is not { Amount: >= 0m })
    {
        throw new BookingValidationException("A booking request needs a declared value.");
    }

    return new BookingRequest
    {
        Origin = _origin,
        Destination = _destination,
        DeclaredValue = _declaredValue.Value,
        ServiceLevel = _serviceLevel,
        LineItems = _lineItems.Count == 0 ? [] : [.. _lineItems],
        InsuranceRequested = _insuranceRequested,
        SpecialInstructions = _specialInstructions,
        CustomerReference = _customerReference,
    };
}
```

Each fluent method (`WithOrigin`, `WithDestination`, `WithDeclaredValue`, `WithServiceLevel`, `AddLineItem`, `RequestInsurance`, `WithSpecialInstructions`, `WithCustomerReference`) returns `this`. A caller can set the origin on one line, add line items inside a loop, and call `Build()` when every branch has had its turn. An object initializer is a single expression, so every value has to be known in one place.

Given the same three required values, the builder and the initializer produce equal requests, because records compare by value.

### Compile time against run time

The two paths catch the same omission at different times:

| Omission | Object initializer | `BookingRequestBuilder` |
| --- | --- | --- |
| No origin | Build error CS9035, with a line number | Compiles; `Build()` throws `BookingValidationException` |
| Blank destination | Compiles; not caught | `Build()` throws |
| Negative declared value | `Money` throws on construction | `Money` throws on construction |

`BookingValidationException` is a `sealed class` with the three constructors analyzer rule CA1032 expects on a custom exception: parameterless, message-only, and message plus inner exception. Use the builder for the validation `required` can't express, not for the presence check `required` already owns.

## Pattern: Prototype

### The problem

A team books the Columbus to Memphis lane every day with the same declared value, surcharge, insurance and dock instructions. Each new booking differs in one or two fields. Re-typing eight fields per booking invites drift, and editing a shared instance in place changes it for every caller.

### The solution

A **Prototype** creates new objects by copying a fully configured existing one and changing only what differs.

### How C# models it

Every record supports a `with` expression: the compiler copies each member from the source and then applies the listed overrides. No `Clone` method is written. `BookingTemplates.StandardDomestic` is the prototype:

{% raw %}
```csharp
public static BookingRequest StandardDomestic { get; } = new()
{
    Origin = "Columbus, OH",
    Destination = "Memphis, TN",
    DeclaredValue = Money.Of(1200.00m),
    ServiceLevel = "standard",
    LineItems = [new LineItem("Fuel surcharge", Money.Of(45.00m), 1)],
    InsuranceRequested = true,
    SpecialInstructions = "Dock door 4, weekday deliveries only.",
    CustomerReference = "WH-COL-MEM",
};
```
{% endraw %}

A clone names only the fields that change:

```csharp
var amended = BookingTemplates.StandardDomestic
    with { ServiceLevel = "express", Destination = "Atlanta, GA" };
```

`amended` is express to Atlanta and keeps the template's origin, declared value and customer reference. The template is still standard to Memphis. That matters because `StandardDomestic` is a `static` property every caller in the process reads: `with` always produces a new object and never writes through the receiver.

### The shallow-copy rule

`with` copies member by member. A value-typed member such as `DeclaredValue`, a `Money`, is duplicated. A reference-typed member such as `LineItems`, an `IReadOnlyList<LineItem>`, is copied as a reference, so the clone and the template share one list.

| Member kind | What `with` copies | Safe to leave unmentioned? |
| --- | --- | --- |
| Value type (`Money`, `bool`) | The value | Yes |
| Immutable reference (`string`) | The reference; nothing can mutate it | Yes |
| Collection or mutable object | The reference; both sides share it | Only if neither side changes it |

`IReadOnlyList<T>` has no `Add`, so appending to the shared list doesn't compile. The fix is to replace the member with a new list, and a collection-expression spread does that in one line:

```csharp
var amended = template with { LineItems = [.. template.LineItems, extraItem] };
```

The spread copies the template's elements into a fresh list and appends `extraItem`. The clone's list is one longer and contains `extraItem`; the template's list keeps its original count and doesn't.

{% include excalidraw.html name="16-with-prototype-shallow-copy" caption="Figure 16.2: A with expression clones BookingTemplates.StandardDomestic and overrides only the named fields, leaving the template untouched. A collection-expression spread replaces the shared LineItems reference with a fresh, independent list." %}

## When not to use it

- **Builder.** Skip it when nothing in `Build()` would ever throw. A builder for a type with no cross-field rule and no staged assembly adds a class, a private field per property and a `Build()` that copies them into the initializer the caller could have written directly.
- **Prototype.** Skip the template when no configuration recurs. A static template for a request built once is indirection a reader traces back to one caller. A type with several mutable reference members needs an explicit replacement on every clone; a constructor or builder that states each member may serve it better.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a booking request.** "Give `BookingRequest` a fluent API" leaves open whether a builder adds any check and whether a clone shares the source's collections.

> Construct `BookingRequest` with an object initializer: `Origin`, `Destination` and `DeclaredValue` are `required` with `init`, and the other members have defaults. Keep `BookingRequestBuilder` only because its `Build()` checks what `required` can't, a non-blank origin and destination, and throws `BookingValidationException`. Derive variants of `BookingTemplates.StandardDomestic` with `with`, and when a variant changes `LineItems`, assign a new collection expression that spreads the template's items, so the clone shares no mutable reference with the template.

Check with `dotnet test --project BookingRequests.Tests --filter BookingTemplatesTests`; `Collection_spread_amend_copies_rather_than_shares_the_template_line_items` must pass.
</blockquote>

## Build, run, observe

```sh
cd examples/16-builder-required-members-prototype-with
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. All 5 tests pass: the initializer path and its defaults, the builder's equality with that path and its exception for a missing origin, and the template's two clone cases.

- **Drop a required member.** Remove `DeclaredValue` from an initializer. The build fails with CS9035.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of
  Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480)
  (Addison-Wesley, 1994): the original statements of Builder and
  Prototype.
- Microsoft Learn, [`required` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required):
  the compile-time CS9035 check.
- Microsoft Learn, [Object and collection initializers](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers):
  object-initializer construction and collection-expression defaults.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records) and
  the [`with` expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression):
  non-destructive, shallow-copy semantics.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
