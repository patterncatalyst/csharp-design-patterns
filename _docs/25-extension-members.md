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

- Write a classic `this`-parameter extension method and know when it's still the right form.
- Declare an `extension(CarrierShipment shipment)` block with instance extension properties and a non-mutating extension method.
- Declare static extension members in an `extension(CarrierShipment)` block and call them through the vendor type's own name.
- Add an extension `operator +` with the named alternate CA2225 expects.

## Why this matters

A sealed type from a package you depend on can be read but never changed: no base class to inherit, no partial declaration to reopen. Extension methods have attached single methods to such types since C# 3. Extension members, new in C# 14, attach a full member set: properties, static members and operators, all from a file the vendor never sees, with call sites that read as though the vendor declared them.

{% include excalidraw.html name="25-extension-members" caption="Figure 25.1: CarrierShipment is a sealed vendor type with only a constructor and three read-only properties. CarrierShipmentExtensions attaches properties, a non-mutating method, static factories, and an operator from outside that file, without CarrierShipment.cs ever changing." %}

## A sealed vendor type

`CarrierShipment` stands in for a carrier SDK's shipment record:

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

`sealed` rules out a derived type, and the missing setters rule out mutation. Everything added from outside reads those three properties or builds a new instance through the public constructor.

The classic form still covers a single method with one receiver:

```csharp
public static string NormalisedDestination(this CarrierShipment shipment) =>
    shipment.DestinationCode.Trim().ToUpperInvariant();
```

The `this` modifier makes the static method callable as `shipment.NormalisedDestination()`. Extension blocks don't deprecate it, and it lives on the same static class as the blocks.

## Instance extension members

A `this`-parameter method can compute a weight in kilograms, but the caller has to write `shipment.WeightKilograms()`. An instance extension block gives real properties:

```csharp
extension(CarrierShipment shipment)
{
    public decimal WeightKilograms => shipment.WeightGrams / 1000m;

    public bool IsHeavy => shipment.WeightGrams > 30_000;

    public CarrierShipment WithAddedWeight(int grams) =>
        new(shipment.WeightGrams + grams, shipment.DestinationCode, shipment.DeclaredValueMinorUnits);
}
```

The parameter on the `extension(...)` clause is the receiver for every member in the block, in the role `this` plays in an instance member. `shipment.WeightKilograms` and `shipment.IsHeavy` read with no parentheses. `IsHeavy` flips above 30,000 grams.

`WithAddedWeight` takes a parameter, so it's a method. It returns a new `CarrierShipment` and leaves the receiver unchanged. With no setters on the type, that's the only option: reassigning the receiver inside the method would only rebind the local parameter.

{% include excalidraw.html name="25-adapting-an-unowned-type" caption="Figure 25.3: CarrierShipment stays frozen and is never mutated by any extension member. WithAddedWeight returns a new instance rather than changing the original, and every extension member adds behavior entirely from outside the vendor type." %}

## Static extension members

Static properties and methods with no receiver instance go in a block that names the type and no parameter:

```csharp
extension(CarrierShipment)
{
    public static CarrierShipment Empty => new(0, string.Empty, 0);

    public static CarrierShipment FromKilograms(decimal kilograms, string destinationCode) =>
        new(decimal.ToInt32(decimal.Round(kilograms * 1000m)), destinationCode, 0);
}
```

Callers write `CarrierShipment.Empty` and `CarrierShipment.FromKilograms(3.2m, "SIN")`, as if the vendor had shipped a default value and a kilogram constructor. A classic extension method can't do this, because its `this` parameter needs an instance. Before extension blocks, such factories lived in a separately named helper class.

`FromKilograms` rounds to the nearest gram because `WeightGrams` is an `int`. `Empty` is a zero-weight, unrouted shipment, useful as a fold seed with the operator below.

## Extension operators and named alternates

An operator is static, but it can sit in the instance block next to the members that share its receiver type:

```csharp
public static CarrierShipment operator +(CarrierShipment left, CarrierShipment right) =>
    new(
        left.WeightGrams + right.WeightGrams,
        left.DestinationCode,
        left.DeclaredValueMinorUnits + right.DeclaredValueMinorUnits);

public static CarrierShipment Add(CarrierShipment left, CarrierShipment right) => left + right;
```

`+` combines two shipments into one manifest line: weights and declared values add, and the destination comes from the left operand. Values built by `FromKilograms` and by the vendor constructor combine the same way, so `CarrierShipment.FromKilograms(1.5m, "AMS") + CarrierShipment.Empty` uses a static extension member and the extension operator in one expression.

`Add` is the named alternate. CA2225 asks every custom operator for a plain static method (`Add` for `+`, `Subtract` for `-`) so callers in a language or tool without operator syntax can reach it. The rule applies to an extension operator exactly as to one declared on the type.

## Two syntaxes, one static class

`CarrierShipmentExtensions` holds three declaration shapes, and all three resolve onto `CarrierShipment` at a call site:

| Shape | Receiver comes from | Holds |
| --- | --- | --- |
| `extension(CarrierShipment shipment)` | the named parameter | instance properties, instance methods, operators |
| `extension(CarrierShipment)` | none | static properties and methods |
| `this CarrierShipment shipment` | the `this` parameter | one classic method |

{% include excalidraw.html name="25-extension-block-and-classic-form" caption="Figure 25.2: CarrierShipmentExtensions mixes three declaration shapes, an instance extension block, a parameterless static extension block, and a classic this-parameter method, and all three resolve onto CarrierShipment at a call site." %}

Pick by what the member needs:

- **One extra method** on one receiver: the classic form. There's no reason to migrate a working one.
- **A property, an operator, or a group of members** sharing a receiver: a block.
- **A member with no instance**: the parameterless block.

One analyzer gap: CA1708, "identifiers should differ by more than case", can fire on the compiler-generated marker members of the two blocks, though the blocks declare disjoint members. `CarrierShipmentExtensions` suppresses CA1708 with a `SuppressMessage` attribute that records the justification. Expect the same in any project that adopts extension blocks before its analyzers catch up.

## When not to use it

- **A type you own.** Add the member to the type. It then appears in the type's own source and completion list, where maintainers look for it.
- **A wide surface on an unowned type.** Extension members show up only when their namespace is imported, so a caller without `using CarrierParcels;` sees none of them. Several static classes each adding members to the same receiver leave no single place to look. For a wide, cohesive surface, a wrapper type around the vendor type keeps everything in one place, at the cost of a construction step at each boundary; an interface over the wrapper helps when the vendor type is one of several substitutes.
- **An operator whose meaning isn't obvious.** `shipment1 + shipment2` reads like built-in arithmetic, with nothing marking it as a locally written rule. The named alternate gives readers a name to search for; if the rule needs explaining, a named method alone may be the better choice.

> **🤖 Explore with your agent**
>
> "`CarrierShipment` in `CarrierParcels.Vendor` is a sealed vendor type with get-only properties. In `CarrierShipmentExtensions.cs`, add `WeightKilograms`, `IsHeavy` and `WithAddedWeight(int grams)`, which returns a new instance, in an `extension(CarrierShipment shipment)` block. Put `Empty` and `FromKilograms` in an `extension(CarrierShipment)` block. Pair `operator +` with a static `Add`, the named alternate CA2225 asks for. Add no wrapper type, no factory class and no reflection."
>
> Run `dotnet test --project CarrierParcels.Tests --filter ExtensionMemberTests`; a new type wrapping `CarrierShipment` in the reply is the defect.

## Build, run, observe

```sh
cd examples/25-extension-members
dotnet test
```

All 10 tests pass; `scripts/verify.sh` runs them with every other example. One experiment:

- **Remove the `SuppressMessage` attribute** on `CarrierShipmentExtensions`. CA1708 fires on the two blocks' marker members and the build fails under warnings-as-errors.

## For deeper coverage

- Microsoft Learn, [Extension members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-members): the `extension(...)` block syntax, instance and static extension members, and how the classic `this`-parameter form coexists with blocks.
- Microsoft Learn, [Operator overloading](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/operator-overloading) and [CA2225: Operator overloads have named alternates](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2225): declaring `operator +` in an extension block and pairing it with `Add`.
- The C# language specification, [Extensions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/extensions): the normative description of extension blocks, introduced in C# 14.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
