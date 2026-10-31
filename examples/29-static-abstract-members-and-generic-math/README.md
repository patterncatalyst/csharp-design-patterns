# 29-static-abstract-members-and-generic-math

Static abstract interface members over a shipping-rate domain, using only the
generic math interfaces that ship in the BCL (`System.Numerics`), no package.

- **`Money`.** A `readonly record struct` carrying a single decimal amount in
  one implicit currency. It implements `IAdditionOperators<Money, Money,
  Money>`, `IAdditiveIdentity<Money, Money>`, `IMultiplyOperators<Money,
  decimal, Money>`, and `IComparisonOperators<Money, Money, bool>`, so `+`,
  `*`, `<`, `<=`, `>`, and `>=` all work on `Money` the same way they work on
  any built-in numeric type. `Add` and `Multiply` are named alternates for the
  two arithmetic operators.
- **`IRateable<TSelf>`.** A book-defined contract shaped like the BCL's own
  generic math interfaces: `static abstract Money BaseRate` and `static
  abstract Money RateOf(TSelf shipment)`, constrained by `where TSelf :
  IRateable<TSelf>`. Three shipment types, `ParcelShipment`, `FreightPallet`,
  and `DocumentEnvelope`, each implement both static members with their own
  formula and their own base rate.
- **`RateBook`.** A generic algorithm written once: `Quote<T>` calls
  `T.RateOf(shipment)` and `BaseRateOf<T>` reads `T.BaseRate`, both through the
  type parameter alone, with no instance needed for the second call and no
  branch on the concrete type for either.
- **`Totals`.** A generic `Sum<T>` constrained to `IAdditionOperators<T, T, T>`
  and `IAdditiveIdentity<T, T>`, folding from `T.AdditiveIdentity` rather than
  a literal zero. The same method runs over a sequence of `Money` and over a
  sequence of built-in `decimal`, since `decimal` already implements
  `INumber<decimal>`.

**What the tests prove.** `MoneyGenericMathTests` exercises each of `Money`'s
generic math operators directly, including a `[Theory]` over the four
comparison operators, then runs `Totals.Sum` over both `Money` and `decimal`
to show the algorithm is not type-specific. `StaticAbstractDispatchTests`
confirms `RateBook.Quote` dispatches to a shipment type's own static `RateOf`,
that three different shipment types each contribute their own base rate
through the same generic call, that `RateBook.BaseRateOf<T>` reads a static
abstract member with no instance in hand, and that `RateBook.Quote` composed
with `Totals.Sum` totals a set of same-typed shipments correctly.

Shipping domain, no containers: pure value types and generic algorithms over
in-memory data, no network or I/O to fake out.

## Build and test

```sh
cd examples/29-static-abstract-members-and-generic-math
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
