---
title: "Extension methods and C# 14 extension members"
order: 25
layout: tutorial
sectionid: docs
part: "idiomatic-csharp"
description: "Extension properties, static extension members, and operators, used to decorate or adapt a type you do not own."
---

<!-- EXAMPLE (shipping domain): Extending a carrier SDK type you do not own, with extension properties and operators. -->

## Learning objectives

By the end of this chapter you can:

- Write a classic `this`-parameter extension method, `NormalisedDestination`,
  and explain why it is still the right form for a single extra method on a
  type you do not own.
- Declare an `extension(CarrierShipment shipment)` block and add instance
  extension properties, `WeightKilograms` and `IsHeavy`, that read from the
  receiver the same way an instance property declared on the type itself
  would.
- Write an instance extension method, `WithAddedWeight`, that returns a new
  `CarrierShipment` instead of mutating the receiver, and explain why that
  shape is required rather than optional for a type with no setters.
- Declare static extension members, `Empty` and `FromKilograms`, inside an
  `extension(CarrierShipment)` block, and call them through the vendor
  type's own name exactly as if `CarrierShipment` had declared them.
- Add an extension `operator +` with a named alternate, `Add`, and explain
  what CA2225 expects from an operator declared outside the type it operates
  on.

## Why this matters

A sealed type from a package you depend on is a type you can read but never
change. `CarrierShipment` in this chapter's example stands in for that
situation directly: a constructor and three read-only properties,
`WeightGrams`, `DestinationCode`, and `DeclaredValueMinorUnits`, shipped by a
carrier's SDK, with no base class to inherit from and no partial declaration
to reopen. Every other chapter in this book adds behaviour to a type it also
owns. This one does not, and the gap between those two situations is where
extension methods have lived since C# 3.

C# has always let a static method declared with a `this` modifier on its
first parameter be called as though it were an instance member: `shipment
.NormalisedDestination()` reads exactly like a method `CarrierShipment`
declared itself, even though it is a static method in a different class in a
different namespace. That classic form covers a single extra method well.
C# 14 broadens what can be attached to a type you do not own from a single
method to a full member set. An `extension(CarrierShipment shipment)` block
groups an instance extension property, an instance extension method, and an
extension operator under one receiver declaration, and a sibling
`extension(CarrierShipment)` block, with no parameter name, adds static
members that are called through `CarrierShipment`'s own name, `CarrierShipment
.Empty` and `CarrierShipment.FromKilograms(...)`, the same call shape a type
author would get by declaring those members directly. None of this requires
touching the vendor's assembly. A property, a non-mutating instance method, a
pair of static factory members, and an operator with its named alternate all
attach to `CarrierShipment` from a file the vendor never sees, and the classic
`this`-parameter form keeps working on the same static class alongside the
new block syntax, so a single extra method does not need to grow into a block
just to join the group.

{% include excalidraw.html name="25-extension-members" caption="Figure 25.1: CarrierShipment is a sealed vendor type with only a constructor and three read-only properties. CarrierShipmentExtensions attaches properties, a non-mutating method, static factories, and an operator from outside that file, without CarrierShipment.cs ever changing." %}

## A sealed vendor type, extended from outside

`CarrierShipment` in the example behind this chapter is kept thin:

```csharp
namespace CarrierParcels.Vendor;

public sealed class CarrierShipment
{
    public CarrierShipment(int weightGrams, string destinationCode, int declaredValueMinorUnits)
    {
        WeightGrams = weightGrams;
        DestinationCode = destinationCode;
        DeclaredValueMinorUnits = declaredValueMinorUnits;
    }

    public int WeightGrams { get; }

    public string DestinationCode { get; }

    public int DeclaredValueMinorUnits { get; }
}
```

`sealed` rules out a derived type, and the absence of setters rules out
mutation after construction. Those two facts together are what make this
type a realistic stand-in for a vendor SDK: a carrier publishes a contract
for what a shipment record looks like, and nothing about that contract
invites a consumer to subclass it or change it in place. Every behaviour this
chapter adds has to work within those constraints, reading the three
properties a `CarrierShipment` already exposes and, where a new value is
needed, building a new instance through the public constructor rather than
reaching past it.

`CarrierShipmentExtensions` is a separate static class, in a separate
namespace, that carries every one of those behaviours. The simplest member on
it is the classic extension method C# has supported since version 3:

```csharp
public static string NormalisedDestination(this CarrierShipment shipment) =>
    shipment.DestinationCode.Trim().ToUpperInvariant();
```

The `this` modifier on the `shipment` parameter is what turns an ordinary
static method into one callable as `shipment.NormalisedDestination()`. This
form stays the right choice for exactly this kind of member: one method,
with one receiver parameter, that reads the receiver and returns a value.
Nothing about C# 14's extension blocks deprecates it, and the example keeps
it on the same static class as the newer syntax specifically to show the two
forms coexisting rather than one replacing the other.

## Instance extension properties and a non-mutating extension method

An extension property is where the classic method form runs out of room. A
`this`-parameter method can compute `WeightKilograms`, but it has to be
called as `shipment.WeightKilograms()`, with parentheses a property read
never carries. C# 14's `extension(CarrierShipment shipment)` block closes
that gap by letting an instance member declared inside it read as a true
property at the call site:

```csharp
extension(CarrierShipment shipment)
{
    public decimal WeightKilograms => shipment.WeightGrams / 1000m;

    public bool IsHeavy => shipment.WeightGrams > 30_000;

    public CarrierShipment WithAddedWeight(int grams) =>
        new(shipment.WeightGrams + grams, shipment.DestinationCode, shipment.DeclaredValueMinorUnits);
}
```

The `shipment` parameter named on the `extension(...)` clause is the receiver
for every member declared inside the block, in exactly the same role `this`
plays inside an ordinary instance method. `WeightKilograms` divides
`shipment.WeightGrams` by a thousand and returns the result as a `decimal`
property, called as `shipment.WeightKilograms` with no parentheses, reading
indistinguishably from a property `CarrierShipment` declared on itself.
`IsHeavy` compares the same `WeightGrams` field against a fixed
thirty-kilogram threshold and returns a `bool`, a second property sharing the
one receiver the block declares once.

`WithAddedWeight` is an instance extension method rather than a property,
because adding weight needs a parameter, `grams`, that a property's getter
has no way to accept. What it returns matters more than its shape: a new
`CarrierShipment`, built from the receiver's current weight plus `grams`,
its existing destination, and its existing declared value, leaving the
`shipment` the method was called on completely untouched. This is not a
style choice the example could have made differently. `CarrierShipment` has
no setters, so there is no field `WithAddedWeight` could assign even if it
tried; the only way to produce a shipment with different weight is to
construct one. An extension method's receiver parameter is passed the same
way any ordinary parameter would be, and reassigning it inside the method
body would only rebind the local parameter, not change anything the caller
holds a reference to, so a shipment adapted through `WithAddedWeight` is
a different object from the one it started from, not a mutated alias. The test
`WithAddedWeight_returns_an_adapted_shipment_and_leaves_the_original_unchanged`
confirms exactly that: it adapts one shipment, checks the new instance's
weight, destination, and declared value, and then re-reads the original
shipment's weight to confirm it never moved.

{% include excalidraw.html name="25-adapting-an-unowned-type" caption="Figure 25.3: CarrierShipment stays frozen and is never mutated by any extension member. WithAddedWeight returns a new instance rather than changing the original, and every extension member adds behavior entirely from outside the vendor type." %}

## Static extension members: building the type you don't own

The same `extension(CarrierShipment shipment)` block mixes instance members
freely with static operators, covered next, but static extension
*properties* and *methods* that have no receiver instance at all need their
own block, one declared with the type name and no parameter:

```csharp
extension(CarrierShipment)
{
    public static CarrierShipment Empty => new(0, string.Empty, 0);

    public static CarrierShipment FromKilograms(decimal kilograms, string destinationCode) =>
        new(decimal.ToInt32(decimal.Round(kilograms * 1000m)), destinationCode, 0);
}
```

`Empty` and `FromKilograms` are called through `CarrierShipment`'s own name,
`CarrierShipment.Empty` and `CarrierShipment.FromKilograms(3.2m, "SIN")`,
exactly as if the vendor type had declared a static factory property and a
static factory method itself. This is the clearest demonstration in the
chapter of what changed in C# 14: a classic extension method can only ever
be called on an instance, because its defining feature is the `this`
parameter an instance supplies at the call site. A static extension member
has no instance to supply, so no amount of creativity with the classic form
reaches it; before extension blocks, a factory for a type you do not own had
to live in its own helper class with its own name, `CarrierShipmentFactory
.Empty` or similar, a workable but visibly bolted-on substitute for the real
thing. `CarrierShipment.Empty` reads as though the vendor had shipped a zero-
weight default and `CarrierShipment.FromKilograms` reads as though the
vendor had shipped a kilogram-based constructor overload, and a caller never
needs to know, or care, that neither one lives in `CarrierShipment.cs`.

`FromKilograms` rounds its `kilograms` argument to the nearest whole gram
before handing it to the constructor, since `CarrierShipment.WeightGrams` is
an `int` and a kilogram value such as `3.2m` has no exact integer gram
representation without that rounding step. `Empty` needs no such conversion;
it is a fixed zero-weight, empty-destination, zero-value shipment, useful as
a starting point for a fold or an accumulation that combines shipments with
the extension operator covered next.

## Extension operators and the named-alternate rule

An operator is itself always a static member, so it belongs in the same kind
of block as any other static behaviour, declared once and resolved at every
`+` a caller writes against two `CarrierShipment` values:

```csharp
public static CarrierShipment operator +(CarrierShipment left, CarrierShipment right) =>
    new(
        left.WeightGrams + right.WeightGrams,
        left.DestinationCode,
        left.DeclaredValueMinorUnits + right.DeclaredValueMinorUnits);

public static CarrierShipment Add(CarrierShipment left, CarrierShipment right) => left + right;
```

`operator +` combines two shipments into a single manifest line: weights and
declared values add together, and the combined destination is carried from
the left-hand operand, since one manifest line travels to one place.
`left + right` in the test suite calls this operator directly, and a second
pair, `fromKilograms + CarrierShipment.Empty`, confirms that a value built by
the static extension member `FromKilograms` combines through the same
operator as a value built by the vendor's own constructor, because both are
ordinary `CarrierShipment` instances once construction finishes.

`Add` sits beside the operator as its named alternate, calling straight
through to `left + right`. CA2225 is the analyzer rule behind that second
method: an operator overload is invisible to a caller working in a language
or a reflection-based tool that has no operator syntax for it, so the rule
asks every custom operator to ship a plain static method with a
conventional name a caller without operator syntax can still reach. `Add`
for `+`, `Subtract` for `-`, and the rest of that naming convention apply to
an extension operator exactly as they do to one declared on the type itself;
C# 14 changes where the operator can be declared, not what the analyzer
expects once it is. Pairing `operator +` with `Add` here costs one line and
keeps `CarrierShipmentExtensions` consistent with the operator convention
this book uses everywhere an operator appears.

## Two syntaxes, one static class

`CarrierShipmentExtensions` ends up holding three distinct declaration
shapes: two `extension(...)` blocks, one for the instance members and one
for the static members, plus the classic `this`-parameter method declared
directly inside the static class, outside either block. All three resolve
onto `CarrierShipment` at a call site, and a reader moving through the file
sees exactly where each member's receiver comes from, the named parameter on
an instance block, the bare type name on a static block, or the `this`
modifier on a classic method's own parameter list.

{% include excalidraw.html name="25-extension-block-and-classic-form" caption="Figure 25.2: CarrierShipmentExtensions mixes three declaration shapes, an instance extension block, a parameterless static extension block, and a classic this-parameter method, and all three resolve onto CarrierShipment at a call site." %}

Choosing between the two forms for a brand new member is mostly a question
of what that member needs. One extra method with one receiver and nothing
static attached to it reads perfectly well as a classic `this`-parameter
method, and nothing about C# 14 asks an author to migrate a working one into
a block just because the block syntax now exists. A property, an operator,
or a group of related members sharing one receiver and one set of static
factories is where the extra ceremony of a block's own declaration pays off,
because it groups what would otherwise be several separate static classes,
or a mix of a static helper class and a `this`-parameter method file, under
one `extension(...)` clause per receiver shape.

One tooling detail is worth knowing before it surprises a reader checking
this example's build output: an analyzer that has not caught up to extension
blocks can flag CA1708, "identifiers should differ by more than case",
comparing the compiler-generated marker members the two `extension(...)`
blocks produce behind the scenes, even though the blocks themselves declare
entirely disjoint member sets, one instance and one static. The example
suppresses that specific rule on `CarrierShipmentExtensions` with a
justification recorded in the attribute, re-verified against the current SDK.
It is a gap in the analyzer's support for a newer piece of the language, not
a defect in the feature, and it is the kind of thing worth checking for in
any project that adopts C# 14 extension blocks ahead of its analyzer
packages catching up.

## Build, run, observe

The example behind this chapter lives in `examples/25-extension-members`. It
is synchronous, allocates no resources beyond ordinary objects, and needs
nothing beyond the local .NET 10 SDK to run. It is standalone-testable from
its own directory:

```sh
cd examples/25-extension-members
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 10
  failed: 0
  succeeded: 10
  skipped: 0
  duration: 725ms
```

`ExtensionMemberTests` covers the instance and static members directly:
`WeightKilograms_converts_grams_to_kilograms` checks the conversion against a
single fixed shipment, a `Theory` sweeps three weights across the thirty-
kilogram threshold to prove `IsHeavy` flips exactly at the boundary, and
`WithAddedWeight_returns_an_adapted_shipment_and_leaves_the_original_unchanged`
confirms the non-mutating contract by re-reading the original shipment after
adapting a copy of it. `Empty_static_member_has_zero_weight` and
`FromKilograms_builds_a_shipment_with_the_expected_grams` check the two
static extension members independently, and
`NormalisedDestination_classic_extension_method_upper_cases_and_trims`
confirms the classic `this`-parameter form still behaves as a plain instance
method would. `ExtensionOperatorTests` adds two further cases:
`Operator_plus_combines_two_shipments_weights_and_values` checks the
operator's arithmetic directly, and
`Extension_members_resolve_on_the_unowned_sealed_type` chains a static
extension member, an instance extension property, and the extension
operator in one test, `CarrierShipment.FromKilograms(1.5m, "AMS") +
CarrierShipment.Empty`, to confirm all three kinds of extension member
interoperate on values of the one sealed type none of them were declared
inside.

## When not to use it

Extension members exist for a type a caller cannot change. A type the same
codebase owns should get a new member added directly rather than through an
extension, instance or static: a member declared on the type itself appears
in that type's own IntelliSense list and its own source file, where a
reviewer or a future maintainer expects to find it, while an extension
member on an owned type adds an indirection with no corresponding benefit.
`WeightKilograms` and `IsHeavy` earn their place as extensions specifically
because `CarrierShipment` is sealed and vendor-owned; the same two members
on a type this codebase controls would read more naturally as properties
declared where the rest of the type's state lives.

Discoverability is the real cost an extension member carries even when the
receiver is truly unowned. A property or method declared on
`CarrierShipment` itself shows up to a reader who opens that one file; an
extension member only shows up once its declaring namespace is in scope,
which means a caller who has not imported `CarrierParcels` alongside
`CarrierParcels.Vendor` will not see `WeightKilograms`, `IsHeavy`, or
`FromKilograms` in completion at all. A large surface of extension members
spread across several static classes, each contributing a few members to the
same receiver type, makes that discovery problem worse: a reader has no
single place to look for everything a `CarrierShipment` can do, the way a
type's own declaration would provide. Where an unowned type needs a wide,
cohesive surface of new behaviour rather than a handful of focused members,
a wrapper type implementing the operations a caller needs, built
around the vendor type rather than extending it, keeps that surface
discoverable in one place at the cost of an explicit construction step at
every boundary. An interface over that wrapper is a cleaner seam still when
the vendor type is only one of several shapes a caller might substitute.

An extension operator carries a sharper version of the same risk. `shipment1
+ shipment2` reads as ordinary arithmetic at the call site, with nothing
marking it as a vendor type's values being combined by a rule this codebase
wrote, not the vendor. A reader unfamiliar with `CarrierShipmentExtensions`
has every reason to assume `+` on a type like `CarrierShipment` either does
not compile or means something the vendor defined, and the surprise of
finding a locally authored manifest-combination rule behind a stock operator
symbol is exactly the kind of implicit behaviour an operator should avoid
introducing. `Add`, the named alternate CA2225 asks for, gives a reader a
second way to find and name that behaviour without relying on recognising an
operator as something other than built-in arithmetic, which is the whole
reason the rule exists and the reason this example pairs every operator with
one.

<blockquote class="ai-sidebar" markdown="1">
Asked to "add a method to this SDK type," an AI pairing
partner working against a sealed, vendor-owned class will sometimes
propose wrapping it in a new owned type that duplicates every property
just to attach one extra method, or reach for reflection to force a field
onto an instance that was never designed to hold one. `CarrierShipment
Extensions` shows the simpler answer for most of that class of request: a
classic `this`-parameter method or an `extension(CarrierShipment shipment)`
block attaches the member directly, with no wrapper type and no copied
properties to keep in sync with the vendor's own. A second common
suggestion, asked for a "static helper for creating these," reaches for a
separate `CarrierShipmentFactory` class with its own name a caller has to
remember, when a static extension member inside an `extension
(CarrierShipment)` block, called through `CarrierShipment.FromKilograms(...)`
itself, gives the same factory behaviour under the vendor type's own name.
Check a generated answer against the two questions this chapter answers
directly: does the new member need an instance, which decides whether it
belongs in the parameterised or the parameterless extension block, and is
a wrapper type solving a cohesion problem a handful of extension
members would not, or just adding a layer an extension could skip.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Extension members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-members): the authority for the `extension(...)` block syntax, instance and static extension properties and methods, and how a classic `this`-parameter extension method coexists with the newer block form.
- Microsoft Learn, [Operator overloading](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/operator-overloading) and [CA2225: Operator overloads have named alternates](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2225): the authority for declaring `operator +` inside an extension block and pairing it with the named alternate, `Add`, this chapter's example ships.
- The C# language specification, [Extensions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/extensions): the normative description of extension blocks introduced in C# 14, the primary source behind this chapter since no book predates the feature.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
