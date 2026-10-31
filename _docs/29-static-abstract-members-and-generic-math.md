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

- Declare `static abstract` members on an interface, `IRateable<TSelf>.BaseRate`
  and `IRateable<TSelf>.RateOf(TSelf shipment)`, and explain what the
  self-referencing constraint `where TSelf : IRateable<TSelf>` buys a generic
  method that neither member could offer on its own.
- Write a generic method, `RateBook.Quote<T>`, that calls `T.RateOf(shipment)`
  through the type parameter alone, and a second, `RateBook.BaseRateOf<T>`,
  that reads `T.BaseRate` without ever constructing an instance of `T`.
- Implement `Money` as a `readonly record struct` against five
  `System.Numerics` interfaces, `IAdditionOperators`, `IAdditiveIdentity`,
  `IMultiplyOperators`, `IComparisonOperators`, and `IComparable<Money>`, and
  state why `Add` and `Multiply` exist as named methods next to `operator +`
  and `operator *`.
- Write one generic algorithm, `Totals.Sum<T>`, constrained to
  `IAdditionOperators<T, T, T>` and `IAdditiveIdentity<T, T>`, and run it
  unchanged over a sequence of `Money` values and a sequence of built-in
  `decimal` values.
- Explain the single-field simplification `Money` makes and what a shipment's
  rate quote would need to add if that simplification were lifted.

## Why this matters

A generic method can already constrain its type parameter to an interface
and call whatever instance members that interface declares. What it could
not do, before `static abstract` interface members, was constrain a type
parameter over operations that belong to the type itself rather than to any
particular instance: a way to compute, a rate to look up, a zero value to
start from. Any algorithm that needed one of those had to take an instance
along for the ride just to reach a method on it, or fall back to a delegate
parameter supplied by the caller at every call site. `static abstract`
interface members close that gap directly. An interface can now declare a
member that every implementing type supplies at the type level, and a
generic method constrained by `where T : ISomeInterface<T>` can call that
member through `T` itself, `T.SomeMember`, with no instance in hand and
no delegate threaded through the call.

Generic math takes the same idea and applies it to arithmetic. Before the
`System.Numerics` interfaces, a method that needed to add a sequence of
numbers either worked against one concrete numeric type, or took a delegate
parameter supplying the addition and the zero value, because there was no
shared contract an `int`, a `decimal`, and a custom value type like a
monetary amount could all satisfy at once. `IAdditionOperators<TSelf, TOther,
TResult>` and `IAdditiveIdentity<TSelf, TResult>` are exactly that shared
contract: any type that implements them, a built-in number or a custom
struct, can be summed, scaled, or compared by one generic method body written
once. This chapter builds both ideas against the same shipping-domain
example: a rate book that quotes several kinds of shipment through a
self-referencing interface, and a monetary type that participates in generic
math so the same summation algorithm totals a sequence of quotes without
caring whether the amounts are `Money` or plain decimals.

{% include excalidraw.html name="29-static-abstract-members-and-generic-math" caption="Figure 29.1: IRateable<TSelf> declares static abstract Money BaseRate and static abstract Money RateOf(TSelf shipment), a contract three concrete shipment types each implement: ParcelShipment, FreightPallet, and DocumentEnvelope. RateBook.Quote<T>(shipment), constrained to where T : IRateable<T>, calls T.RateOf(shipment) and reaches whichever concrete type's static member the caller supplied, entirely through the type parameter, with RateBook.BaseRateOf<T>() reading T.BaseRate the same way without constructing an instance at all." %}

## A static contract reached through the type parameter

The example behind this chapter lives in
`examples/29-static-abstract-members-and-generic-math`. `IRateable<TSelf>` is
the contract a shipment type opts into, and both members it declares are
`static abstract`:

```csharp
public interface IRateable<TSelf> where TSelf : IRateable<TSelf>
{
    static abstract Money BaseRate { get; }

    static abstract Money RateOf(TSelf shipment);
}
```

`static abstract` marks a member that belongs to the implementing type
itself rather than to any instance of it. An ordinary abstract member on an
interface is something every instance can be asked for through a reference
typed as the interface; a `static abstract` member is something every
implementing type must supply, reachable only through the type itself, not
through an instance reference typed as `IRateable<TSelf>`. `BaseRate` is a
flat rate a shipment type charges before any shipment-specific surcharge, and
`RateOf` computes a full quote for one instance, but neither member is called
on an object the way a conventional interface method would be. They are
called on the type.

The constraint on the interface's own type parameter, `where TSelf :
IRateable<TSelf>`, is what makes that call possible from inside a generic
method. Read literally, it says a type implementing `IRateable<TSelf>` must
supply `TSelf` as itself: `ParcelShipment` implements `IRateable<ParcelShipment>`,
not `IRateable<FreightPallet>` or some other type. That self-reference
is what lets `RateOf`'s parameter be typed as `TSelf` rather than as the
interface, and it is what lets a generic method that only knows `T : IRateable<T>`
call `T.RateOf(shipment)` and know the `shipment` argument's type lines up
with whichever concrete type the caller supplied for `T`. Each concrete type
supplies its own version of both members:

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

`FreightPallet` and `DocumentEnvelope` implement the same contract with their
own rate schedules: `FreightPallet.BaseRate` is `35.00m` and `RateOf` adds a
per-kilogram surcharge plus a per-pallet handling fee, and
`DocumentEnvelope.BaseRate` is `2.00m` with `RateOf` adding a flat per-page
fee. Nothing about `IRateable<TSelf>` forces the three rate formulas to look
alike. The only requirement is that each type name itself as `TSelf` and
supply a `BaseRate` and a `RateOf` that type-checks against its own shape.

`RateBook` is the generic algorithm written once against that contract:

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

`Quote<T>` never branches on what `T` is. It calls `T.RateOf(shipment)`,
and the call resolves to whichever concrete type's `RateOf` the caller's
choice of `T` points at: passing a `ParcelShipment` reaches
`ParcelShipment.RateOf`, passing a `FreightPallet` reaches
`FreightPallet.RateOf`, with the same one line of code in `Quote<T>` doing
the dispatch both times. `BaseRateOf<T>` goes further and needs no shipment
instance at all. `T.BaseRate` is a type-level lookup, so a caller can ask
`RateBook.BaseRateOf<DocumentEnvelope>()` for the document rate schedule's
base charge without building a `DocumentEnvelope` to ask it from. That is
the capability `static abstract` interface members add: a generic method can
reach a type-level operation through its type parameter the same way an
ordinary generic method reaches an instance operation through a parameter
value, with the type itself standing in for the instance.

`StaticAbstractDispatchTests` checks all three shipment types through the
same two `RateBook` methods. `Quote_dispatches_to_the_concrete_types_static_RateOf`
quotes a ten-kilogram `ParcelShipment` through `RateBook.Quote` and asserts
the result equals both `new Money(12.00m)` and a direct call to
`ParcelShipment.RateOf`, confirming the generic path and the direct path
agree. `Quote_uses_each_types_own_base_rate` quotes a zero-weight
`ParcelShipment`, a zero-weight and zero-pallet `FreightPallet`, and a
zero-page `DocumentEnvelope`, each of which strips its formula down to a bare
`BaseRate`, and asserts the three results differ from each other, proving
`RateBook.Quote<T>` reads a different static member per `T` rather than one
shared constant baked into the generic method.
`BaseRateOf_reads_a_static_abstract_member_through_the_type_parameter` calls
`RateBook.BaseRateOf<ParcelShipment>()`, `BaseRateOf<FreightPallet>()`, and
`BaseRateOf<DocumentEnvelope>()` directly and checks each against the
matching type's own `BaseRate`, with no shipment instance constructed
anywhere in the test.

## Generic math: one algorithm, two numeric types

`Money` carries exactly one field, a `decimal Amount`, under a documented
simplification: a realistic rate book would carry a currency code alongside
the amount, but this chapter keeps `Money` to one field so the generic math
interfaces it implements stay the entire point of the example rather than
competing with currency-conversion logic for the reader's attention. As a
`readonly record struct`, `Money` already gets value equality, an immutable
`Amount`, and a generated `ToString` from the record feature already; what
it adds on top is a set of `System.Numerics` interfaces that make it
participate in the same generic arithmetic the built-in numeric types
support:

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

`IAdditionOperators<Money, Money, Money>` requires `operator +` between two
`Money` values producing a third, and `IAdditiveIdentity<Money, Money>`
requires a static `AdditiveIdentity` property, the zero a summation starts
from. `IMultiplyOperators<Money, decimal, Money>` requires `operator *`
scaling a `Money` by a plain `decimal` factor, which is how a quote's base
rate becomes a line charge once a weight or a page count is known.
`IComparisonOperators<Money, Money, bool>` requires the four ordering
operators, `<`, `<=`, `>`, and `>=`, all returning `bool`, and
`IComparable<Money>` requires `CompareTo`, giving `Money` both the operator
syntax and the `IComparable` contract a sort or a `Min`/`Max` call would
expect. `Add` and `Multiply` sit alongside `operator +` and `operator *` as
named method alternates. Roslyn's CA2225 analyzer flags an overloaded
operator that has no named equivalent, because not every caller or every
language feature reaches for operator syntax; `Add` and `Multiply` give
`Money` an explicit method a caller can invoke the same way as any other
method, `Money.Add(a, b)` reading identically to how a caller would invoke
it through reflection or through a delegate without operator syntax at all,
while the operators themselves stay the normal spelling for ordinary
arithmetic in a formula.

{% include excalidraw.html name="29-generic-math-over-money" caption="Figure 29.2: Money implements IAdditionOperators<Money, Money, Money>, IAdditiveIdentity<Money, Money>, IMultiplyOperators<Money, decimal, Money>, IComparisonOperators<Money, Money, bool>, and IComparable<Money> from System.Numerics. Totals.Sum<T>(values), constrained to IAdditionOperators<T, T, T> and IAdditiveIdentity<T, T>, seeds its total from T.AdditiveIdentity and folds with total += value, the same method body producing a Money total from an IEnumerable<Money> and a decimal total from an IEnumerable<decimal>, since decimal already implements INumber<decimal>." %}

`Totals.Sum<T>` is the generic algorithm those interfaces exist to make
possible:

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

The constraint `where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>`
names exactly the two capabilities the method body uses: a type-level zero
to start from, and an addition operator to fold values into that zero one at
a time. `T.AdditiveIdentity` is the same kind of type-level lookup
`RateBook.BaseRateOf<T>` performs, a static member read through the type
parameter rather than through an instance, and `total += value` compiles
because the constraint guarantees `operator +` exists for `T`. Nothing in
`Sum<T>` mentions `Money` or `decimal` by name. The built-in `decimal` type
already implements `INumber<decimal>`, and `INumber<TSelf>` itself extends
`IAdditionOperators<TSelf, TSelf, TSelf>` and `IAdditiveIdentity<TSelf,
TSelf>` among the other generic math interfaces it aggregates, so `decimal`
satisfies `Sum<T>`'s constraint without a single line changed in either type.
The same method body produces a `Money` total from an
`IEnumerable<Money>` and a `decimal` total from an `IEnumerable<decimal>`.

`MoneyGenericMathTests` exercises each interface `Money` implements in turn.
`Money_adds_through_the_addition_operator` adds a `Money(10.00m)` and a
`Money(5.25m)` and checks the `15.25m` sum.
`Money_additive_identity_is_zero` checks `Money.AdditiveIdentity` against
`new Money(0m)` directly. `Money_scales_by_a_decimal_factor` multiplies a
`Money(10.00m)` unit rate by `3m` and checks `30.00m`.
`Money_comparison_operators_order_amounts` is a `Theory` over three pairs of
amounts, checking all four comparison operators against their expected
booleans in one table rather than four separate test methods.
`Sum_totals_a_sequence_of_money` sums three `Money` charges and checks the
`4.50m` total. `Sum_totals_a_sequence_of_decimals` is the proof that
`Totals.Sum<T>` is not `Money`-specific: it sums a plain `decimal[]` of
`1.5m`, `2.25m`, and `0.75m` through the identical `Totals.Sum` call and
checks the same `4.50m` total, instantiated over a built-in type the method
was never written with in mind.

`StaticAbstractDispatchTests.Total_quote_over_a_set_sums_each_shipments_rate`
ties both halves of the chapter together in one test. It quotes three
`ParcelShipment` instances through `RateBook.Quote`, projecting each one to
its `Money` quote with `shipments.Select(RateBook.Quote)`, and folds the
resulting sequence of `Money` values with `Totals.Sum`, asserting the final
total against `new Money(22.50m)`. The static-abstract dispatch in
`RateBook.Quote<T>` and the generic-math fold in `Totals.Sum<T>` are two
separate capabilities, one reaching a type-level operation through a type
parameter and the other constraining a type parameter over arithmetic
operators, and this test is where a single pipeline uses both: a type-level
rate lookup feeding a sequence into a type-level summation, with neither
generic method needing to know `Money` or the three shipment types by name
beyond the constraints each declares.

## Build, run, observe

Every type in this example, `IRateable<TSelf>` and its three implementing
shipment records, `RateBook`, `Money`, and `Totals`, is synchronous and
in-memory, so the suite needs nothing beyond the local .NET 10 SDK. The
example is standalone-testable from its own directory:

```sh
cd examples/29-static-abstract-members-and-generic-math
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 12
  failed: 0
  succeeded: 12
  skipped: 0
  duration: 713ms
```

The twelve cases split across the chapter's two halves.
`StaticAbstractDispatchTests` proves the static-abstract half: dispatch
through `RateBook.Quote<T>` lands on each concrete type's own `RateOf`,
zero-volume shipments expose each type's distinct `BaseRate`,
`RateBook.BaseRateOf<T>` reads a base rate with no instance constructed, and
a projected-then-summed sequence of quotes exercises both generic methods in
one pipeline. `MoneyGenericMathTests` proves the generic-math half: the
addition operator, the additive identity, the multiplication operator, a
`Theory` over all four comparison operators, and two calls to the same
`Totals.Sum<T>` body, one instantiated over `Money` and one instantiated
over the built-in `decimal`, landing on matching totals from two different
types satisfying one constraint.

## When not to use it

A `static abstract` interface member earns its cost when more than one type
needs to supply the same type-level operation and a generic algorithm needs
to reach it without an instance in hand. A single shipment type with no
plan to add a second rate schedule gets nothing from `IRateable<TSelf>` that
a plain static method on that one type would not already provide; the
self-referencing constraint and the generic `RateBook.Quote<T>` wrapper only
pay off once `RateBook` needs to treat `ParcelShipment`, `FreightPallet`,
and `DocumentEnvelope` the same way without knowing which one it has. An
instance member reached through an ordinary, non-generic interface is the
simpler choice whenever an instance is already on hand at every call site
and nothing about the operation needs to run before an instance exists.

Generic math constraints add real complexity to a method signature, and
that cost is easiest to see in `Totals.Sum<T>`'s own constraint list,
`where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>`. A method
written against one concrete numeric type, a `Sum` that only ever takes a
`decimal[]`, states its requirement in one word instead of two interface
names with their own generic arguments spelled out, and a reader checking
what the method needs from `T` has one familiar type to think about instead
of a constraint to parse. Reaching for the full generic math interface set
when a second numeric type will never appear adds signature noise
for a reuse case that never materializes; the constraint is worth writing
down once an algorithm is proven to need it over more than one type, as
`Totals.Sum<T>` does by running over both `Money` and `decimal` in this
chapter's own tests.

`Money`'s single-field simplification is itself a scope boundary worth
naming before extending this example. Adding a currency code would change
every operator and every comparison: `operator +` between two `Money`
values in different currencies cannot sum to a meaningful amount without
either forcing one conversion rate or throwing, and `IComparisonOperators`
would need the same decision before `<` or `>` could mean anything across
currencies. None of the generic math interfaces this chapter uses solve that
problem; they describe what operations a type supports, not what a type
does when two instances of it disagree about units. A rate book handling
multiple currencies needs that policy decided and encoded before `Money`
grows a second field, not left for the compiler to infer from an interface
list.

> **AI sidebar.** Asked to make a type "support math," an AI pairing partner
> will sometimes implement every `System.Numerics` interface a type could
> conceivably satisfy, including ones like `IDivisionOperators` or
> `IUnaryNegationOperators` that nothing in the surrounding code calls,
> because the interface exists and the type is numeric-shaped. Check a
> generated answer against what `Money` needs: this chapter's
> `Money` implements exactly the five interfaces `RateBook` and `Totals`
> exercise, `IAdditionOperators`, `IAdditiveIdentity`,
> `IMultiplyOperators`, `IComparisonOperators`, and `IComparable<Money>`,
> and nothing else, because a quote is added, scaled by a rate, and
> compared, but never divided or negated anywhere in the example. A related
> suggestion skips the `Add` and `Multiply` named alternates entirely and
> leaves CA2225 unaddressed, treating the analyzer warning as cosmetic
> rather than as a signal that an operator-only API has no plain-method
> entry point for a caller that cannot or does not want to use operator
> syntax. A third pattern to watch for is a generic method reaching for the
> full `INumber<T>` constraint when only addition and an identity value are
> ever used, pulling in comparison, parsing, and formatting requirements a
> type implementing a narrower, purpose-built constraint like
> `Totals.Sum<T>`'s would otherwise have no reason to satisfy.

## For deeper coverage

- Microsoft Learn, [Static abstract members in interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/static-abstract-interface-members): the authority for `static abstract` interface members and the self-referencing generic constraint `IRateable<TSelf>` and `RateBook.Quote<T>` build on in this chapter.
- Microsoft Learn, [Generic math](https://learn.microsoft.com/en-us/dotnet/standard/generic-math): the authority for the `System.Numerics` interfaces, `IAdditionOperators`, `IAdditiveIdentity`, `IMultiplyOperators`, `IComparisonOperators`, and `INumber<TSelf>`, that `Money` implements and `Totals.Sum<T>` is constrained against.
- The C# language specification, [Interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/interfaces): the authority for static interface member declarations and how an implementing type satisfies them.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
