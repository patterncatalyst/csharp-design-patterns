---
title: "static abstract members and generic math"
order: 29
layout: tutorial
sectionid: docs
part: "idiomatic-csharp"
description: "Constraining a generic over type-level operations with abstract static interface members and generic math over a custom value type."
---

<!-- EXAMPLE (shipping domain): IRateable<T> with static abstract members over Money. -->

## Learning objectives

By the end of this chapter you can:

- Declare `static abstract` members on a self-referencing interface, `IRateable<TSelf>`, and say what `where TSelf : IRateable<TSelf>` buys.
- Call a type-level member through a type parameter, `T.RateOf(shipment)` and `T.BaseRate`, with no instance for the base rate.
- Implement `Money` against the `System.Numerics` operator interfaces, with named alternates for its operators.
- Write one generic algorithm, `Totals.Sum<T>`, that runs unchanged over `Money` and `decimal`.
- Name what `Money`'s single-field simplification leaves out.

## Why this matters

A generic method constrained to an interface can call that interface's instance members. Before `static abstract` interface members, it couldn't constrain over operations that belong to the type: a rate schedule, a zero value, an addition operator. An algorithm needing one had to carry an instance just to reach it, or take a delegate at every call site. Now an interface declares a member each implementing type supplies at the type level, and a generic method calls it as `T.Member`.

Generic math applies the same idea to arithmetic. `IAdditionOperators<TSelf, TOther, TResult>` and `IAdditiveIdentity<TSelf, TResult>` are a contract that a built-in number and a custom money type can both satisfy, so one method body sums either.

{% include excalidraw.html name="29-static-abstract-members-and-generic-math" caption="Figure 29.1: Three shipment types each supply their own static BaseRate and RateOf through a shared interface. A generic rate book dispatches to the right one purely through its type parameter, with no instance required." %}

## A static contract reached through the type parameter

```csharp
public interface IRateable<TSelf> where TSelf : IRateable<TSelf>
{
    static abstract Money BaseRate { get; }

    static abstract Money RateOf(TSelf shipment);
}
```

A `static abstract` member belongs to the implementing type, not to an instance, and can't be reached through a reference typed as the interface. `BaseRate` is the flat charge before any surcharge; `RateOf` quotes one shipment.

The self-referencing constraint makes each implementer name itself: `ParcelShipment` implements `IRateable<ParcelShipment>`. That lets `RateOf` take a `TSelf` rather than the interface, so a generic method that knows only `T : IRateable<T>` can pass a `T` to `T.RateOf` and the types line up.

```csharp
public sealed record ParcelShipment(decimal WeightKilograms) : IRateable<ParcelShipment>
{
    private const decimal PerKilogramSurcharge = 0.75m;

    public static Money BaseRate => new(4.50m);

    public static Money RateOf(ParcelShipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return BaseRate + new Money(shipment.WeightKilograms * PerKilogramSurcharge);
    }
}
```

The other two implementers use their own formulas:

| Type | `BaseRate` | `RateOf` adds |
| --- | --- | --- |
| `ParcelShipment` | 4.50 | 0.75 per kg |
| `FreightPallet` | 35.00 | a per-kg surcharge and a per-pallet handling fee |
| `DocumentEnvelope` | 2.00 | a flat per-page fee |

The interface requires only that each type name itself as `TSelf` and supply both members.

```csharp
public static class RateBook
{
    public static Money Quote<T>(T shipment) where T : IRateable<T>
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return T.RateOf(shipment);
    }

    public static Money BaseRateOf<T>() where T : IRateable<T> => T.BaseRate;
}
```

`Quote<T>` never branches on `T`: the call resolves to the concrete type's `RateOf`. A 10 kg parcel quotes at 12.00 through `RateBook.Quote`, the same as calling `ParcelShipment.RateOf` directly. `BaseRateOf<DocumentEnvelope>()` returns the document base charge without constructing a `DocumentEnvelope`: the type stands in for the instance.

{% include excalidraw.html name="29-irateable-static-dispatch" caption="Figure 29.3: Three shipment types supply their own rate schedule through a shared static contract. The rate book dispatches to the correct one by type parameter alone, then sums the resulting quotes into a single total." %}

## Generic math: one algorithm, two numeric types

`Money` holds one field, `decimal Amount`. A real rate book would carry a currency code too; leaving it out keeps the generic math interfaces in focus. As a `readonly record struct` it already has value equality and immutability. It adds the operator interfaces:

```csharp
public readonly record struct Money(decimal Amount) :
    IAdditionOperators<Money, Money, Money>,
    IAdditiveIdentity<Money, Money>,
    IMultiplyOperators<Money, decimal, Money>,
    IComparisonOperators<Money, Money, bool>,
    IComparable<Money>
{
    public static Money AdditiveIdentity => new(0m);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator *(Money left, decimal factor) => new(left.Amount * factor);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Multiply(Money left, decimal factor) => left * factor;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);
}
```

| Interface | Requires | Used for |
| --- | --- | --- |
| `IAdditionOperators<Money, Money, Money>` | `operator +` | summing quotes |
| `IAdditiveIdentity<Money, Money>` | static `AdditiveIdentity` | the zero a sum starts from |
| `IMultiplyOperators<Money, decimal, Money>` | `operator *` by a `decimal` | scaling a unit rate by weight or pages |
| `IComparisonOperators<Money, Money, bool>` | `<`, `<=`, `>`, `>=` | ordering quotes |
| `IComparable<Money>` | `CompareTo` | sorting, `Min`, `Max` |

`Add` and `Multiply` are the named alternates CA2225 asks for, so callers without operator syntax, or working through reflection or a delegate, have a plain method.

{% include excalidraw.html name="29-generic-math-over-money" caption="Figure 29.2: Money implements the System.Numerics arithmetic interfaces alongside the built-in decimal type. One generic summation method folds either sequence into a total, seeded from each type's own additive identity." %}

{% raw %}
```csharp
public static class Totals
{
    public static T Sum<T>(IEnumerable<T> values) where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        ArgumentNullException.ThrowIfNull(values);

        var total = T.AdditiveIdentity;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }
}
```
{% endraw %}

The constraint names exactly what the body uses: a type-level zero and an addition operator. `T.AdditiveIdentity` is the same kind of type-level read as `T.BaseRate`. `decimal` implements `INumber<decimal>`, which extends both interfaces, so `decimal` satisfies the constraint with no change to either type. Summing `1.5m`, `2.25m` and `0.75m` gives 4.50 as `decimal`; summing three `Money` charges of the same amounts gives `Money(4.50m)`.

The two capabilities compose. `shipments.Select(RateBook.Quote)` projects three parcels to `Money` quotes through static-abstract dispatch, and `Totals.Sum` folds them to `Money(22.50m)`. Neither generic method names `Money` or a shipment type beyond its constraints.

## When not to use it

- **One implementer.** A single shipment type with no second rate schedule needs a plain static method. `IRateable<TSelf>` and `RateBook.Quote<T>` pay off once several types must be treated alike without knowing which one is in hand. If an instance is always available, an ordinary instance interface is simpler.
- **One numeric type.** A `Sum` that only ever takes `decimal[]` states its requirement in one word. `where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>` is worth writing once a second type actually uses the algorithm, as `Money` and `decimal` do here.
- **Money with currencies.** A currency code changes every operator: adding two currencies needs a conversion policy or an exception, and so does comparing them. The interfaces say which operations exist, not what happens when operands disagree about units. Decide that policy before `Money` grows a second field.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a numeric type.** "Make `Money` support math" doesn't say which operations, and invites every `System.Numerics` interface the type could satisfy. State the operations callers use:

> Make `Money` a `readonly record struct` over `decimal Amount` implementing `IAdditionOperators<Money, Money, Money>`, `IAdditiveIdentity<Money, Money>`, `IMultiplyOperators<Money, decimal, Money>`, `IComparisonOperators<Money, Money, bool>` and `IComparable<Money>`, with `Add` and `Multiply` as named alternates. Add no division or negation. Constrain `Totals.Sum<T>` to `IAdditionOperators<T, T, T>` and `IAdditiveIdentity<T, T>`, not `INumber<T>`, so it sums both `Money` and `decimal`.

Run `dotnet test --project RateMath.Tests --filter MoneyGenericMathTests`; `IDivisionOperators` or `INumber<T>` in the reply is the defect.
</blockquote>

## Build, run, observe

```sh
cd examples/29-static-abstract-members-and-generic-math
dotnet test
```

All 12 tests pass; `scripts/verify.sh` runs them with every other example. One experiment:

- **Drop an interface.** Remove `IAdditiveIdentity<Money, Money>` from `Money`'s base list. The build fails with CS0315 wherever `Totals.Sum` is called with `Money`, because `Money` no longer satisfies the constraint, even though the `AdditiveIdentity` property is still there.

## For deeper coverage

- Microsoft Learn, [Static abstract members in interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/static-abstract-interface-members): `static abstract` interface members and the self-referencing generic constraint.
- Microsoft Learn, [Generic math](https://learn.microsoft.com/en-us/dotnet/standard/generic-math): the `System.Numerics` interfaces, including `IAdditionOperators`, `IAdditiveIdentity`, `IMultiplyOperators`, `IComparisonOperators` and `INumber<TSelf>`.
- The C# language specification, [Interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/interfaces): static interface member declarations and how an implementing type satisfies them.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
