---
title: "NRTs, records, and init/required/with"
order: 9
layout: tutorial
sectionid: docs
part: "dotnet-substrate"
description: "Immutability as a design tool, and why nullable reference types remove the motivation for the Null Object pattern."
---

<!-- EXAMPLE (shipping domain): Consignment as a record, with the with expression for amendments. -->

## Learning objectives

By the end of this chapter you can:

- Say what equality a record class generates, and how it recurses through nested records and record structs.
- Pair `required` with `init` so every non-nullable property is set before construction completes.
- Amend an immutable record with `with`, including one nested level deep, and say why `with` is a shallow copy.
- Tell a legitimately absent member (`string?`) from one that must never be `null`, and why that removes the reason for a Null Object.
- Name the compile error for a missing `required` member, and why the guarantee belongs to the compiler.

## Why this matters

A consignment changes after booking. A broker corrects the declared weight after a re-weigh, a dispatcher re-routes around a closed border crossing, a customer sends a purchase-order number two days later. These are amendments, not new consignments. With a mutable class passed by reference, a setter call in one place changes what every other holder sees: a rating engine that captured the weight for billing has no record of what it billed against.

An immutable `Consignment` removes that failure. Correcting the weight produces a second value that differs in one field, and the first value, with everyone holding it, is unaffected. The only way to get a different `Consignment` is to ask for one.

The other half is what a `Consignment` may hold. A `ConsignmentId` and a `Route` must always be present; a shipment with no route has nowhere to go. A `ReferenceNumber` may never arrive. Nullable reference types put that distinction in the type, and `required` closes the gap they leave: a property the compiler knows can't be `null` but that nothing forces a caller to set.

{% include excalidraw.html name="09-nrts-records-init-required-with" caption="Figure 9.1: Amending a consignment record copies it into a new instance with only the named members overridden, leaving the original untouched; equality compares values, not references." %}

## Records and recursive value equality

`Consignment` is a `sealed record`: a reference type, so assignment copies a reference, with compiler-generated structural equality on top.

```csharp
public sealed record Consignment
{
    public required string ConsignmentId { get; init; }
    public required Route Route { get; init; }
    public required Weight Weight { get; init; }
    public string? ReferenceNumber { get; init; }
    public Money DeclaredValue { get; init; } = Money.Zero;
}
```

Five members with three different rules (three `required`, one nullable, one defaulted) read better as named properties than as positional parameters a caller must get in order. A short positional record suits `Route(string Origin, string Destination)`. Either shape gets `Equals(Consignment?)`, `IEquatable<Consignment>`, `GetHashCode`, `==`, `!=` and a copy constructor, derived from the declared members. Two separately constructed consignments with the same data are equal through `Equals` and `==` and share a hash code.

Equality recurses. `Route` is a positional record and `Weight` and `Money` are `readonly record struct`s, each with its own value equality, so changing only `Route.Destination` or only `Weight.Kilograms` makes two otherwise identical consignments unequal.

The recursion is only as deep as each member's own `Equals`. A `CustomsDeclaration` class with no `record` modifier and no override compares by reference, so two consignments holding field-identical but separately built declarations would compare unequal. The fix is to make every member that takes part in comparison a record or a type with its own value equality. The [record types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records) says generated equality compares "the values of all the fields and properties of the object."

## required and init: closing the construction gap

Nullable reference types warn when a constructor doesn't assign a non-nullable property, but a warning can be ignored, and the property can still be `null` at runtime. The older fix was a constructor taking every non-nullable member as a parameter, kept in sync by hand.

`required` makes every object initializer set the property, and omitting one is an error, not a warning. `ConsignmentId`, `Route` and `Weight` are `required`, with no constructor:

```csharp
// CS9035: Required member 'Consignment.Route' must be set.
var broken = new Consignment
{
    ConsignmentId = "CNS-4217",
    Weight = new Weight(1250m),
};
```

The failure happens at compile time, before anything can run. An initializer that sets all three compiles, and the two members it leaves out fall back to `null` (`ReferenceNumber`) and `Money.Zero` (`DeclaredValue`).

`init` answers a different question: whether the value can change after construction. An `init` accessor "assigns a value to the property or the indexer element only during object construction" ([`init` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init)), and `consignment.Route = newRoute` from outside is a compile error. Every property on `Consignment` uses `init`, so no code can change a field in place.

`DeclaredValue` is non-nullable but not `required`, because `Money.Zero` is a legitimate value for a consignment nobody has valued yet. The line `required` draws is between a member with a type-owned default and one with no sensible value until a caller supplies it:

| Member | Declared | Why |
| --- | --- | --- |
| `ConsignmentId`, `Route`, `Weight` | `required`, non-nullable | No placeholder would be correct. |
| `DeclaredValue` | non-nullable, defaults to `Money.Zero` | Zero is a real value. |
| `ReferenceNumber` | `string?` | Absence is an ordinary state. |

{% include excalidraw.html name="09-required-init-construction-gap" caption="Figure 9.2: An object initializer that sets every required member compiles; one that omits a required member fails at compile time instead of risking a runtime null." %}

## Non-destructive amendment with with

Each amendment is one `with` expression that returns a new value:

```csharp
public Consignment ReRouteTo(string destination) =>
    this with { Route = Route with { Destination = destination } };

public Consignment WithCorrectedWeight(Weight weight) =>
    this with { Weight = weight };

public Consignment WithReferenceNumber(string referenceNumber) =>
    this with { ReferenceNumber = referenceNumber };
```

Re-routing from Hamburg to Antwerp gives a consignment whose `Route.Destination` is `"Antwerp"`, and the original still reads `"Hamburg"`. A one-off amendment needs no helper: `original with { DeclaredValue = new Money(18_500m, "EUR") }` at the call site does the same thing.

In `ReRouteTo` the inner `with` runs first and produces a new `Route` with only `Destination` changed; that `Route` becomes the outer `with`'s replacement for `Consignment.Route`. The [`with` expression documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression) describes both levels the same way: call the synthesized copy constructor, then apply the assignments.

The same page says "the result of a `with` expression is a shallow copy." A reference-typed member is copied as a reference. That's safe here because `Route` is an immutable record: original and amended copy share a `Route` that can't change. If `Route` were a class with `get; set;`, both consignments would point at one mutable object and a change through either would show in both, the aliasing bug immutability was meant to remove. Modeling `Route`, `Weight` and `Money` as records is what makes `with`-based amendment sound.

Re-routing a consignment back to its original destination gives a value equal to the original through `==`, though it's a distinct object.

## Nullable reference types: absence versus never

`ReferenceNumber` is `string?`. A new consignment has no purchase-order reference, and that's an ordinary state: it reads `null` until `WithReferenceNumber` supplies one. The [nullable reference types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types) describes the annotations as helping "you find potential null reference errors"; here the `?` tells every caller that checking for `null` is the contract.

`ConsignmentId`, `Route` and `Weight` have no `?`, and `required` enforces it. A chain of amendments can't leave them `null`, because every `with` starts from a valid `Consignment` and overrides only the members it names.

Together they replace the Null Object pattern. Without them, a team gave `Route` a static `Route.None` or `Route.Empty` sentinel so that `route.Origin` never needed a null check. That earns its cost when the type system can't tell "always present" from "might be absent." With nullable reference types and `required` it can:

- **`Route` is `required`**, so it is never missing and needs no sentinel.
- **`ReferenceNumber` is `string?`**, so absence is `null`, checked once at the point of use, not a placeholder pretending to be a reference number.

## Build, run, observe

```sh
cd examples/09-nrts-records-init-required-with
dotnet test
```

All 15 tests pass, and `scripts/verify.sh` runs them with every other example. They cover recursive equality, every amendment leaving its original unchanged, `ReferenceNumber` before and after an amendment, and the defaults an initializer falls back to. The missing-`required` case has no test because it doesn't compile.

## When not to use it

- **Identity-bearing entities.** A consignment tracked live in a warehouse system, with a database identity, a current location and an audit history, needs identity semantics. Two snapshots of the same shipment at different moments compare unequal under structural equality. An ORM change tracker needs one mutable instance per row to watch, and a type that returns a new value on every change gives it nothing. Use a class compared by a stable key.
- **Mixed mutable graphs.** `with` is shallow. A record that holds an ordinary mutable class shares that object between original and copy, and a later mutation leaks into both. Keep every member that takes part in `with` immutable, or don't rely on the copy being independent.
- **Hot amendment loops.** Each `with` allocates a new `Consignment`. Replaying ten thousand weight corrections in a tight loop allocates ten thousand objects, nearly all garbage at once. For a value amended a few times in its life that's immaterial; for one reprocessed many times a second, an encapsulated mutable type with private setters and invariant-preserving methods can be the better trade.

<blockquote class="ai-sidebar" markdown="1">
**Specifying construction rules.** "Fix the nullable warning" leaves open whether the member is optional or must always be set, and a `?` or `= null!` silences the warning without answering it. State which:

> In `Consignment`, mark `ConsignmentId`, `Route` and `Weight` as `required` with `init` accessors, make `ReferenceNumber` a `string?` and default `DeclaredValue` to `Money.Zero`. Don't add `null!`, a `?` to a required member, or a `None`/`Empty` sentinel. Write amendments as `with` expressions that return a new `Consignment`.

If the reply adds a sentinel object for an optional member, ask why `string?` doesn't fit.
</blockquote>

## For deeper coverage

- The official Microsoft Learn documentation:
  [C# record types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records),
  [Records - C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record),
  [The `init` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init),
  [The `required` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required),
  [The `with` expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression),
  and [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types).
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
