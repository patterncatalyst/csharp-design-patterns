# 40-anti-patterns-and-pattern-misuse

The same freight rate quote, computed two ways, to make over-abstraction
visible rather than merely described.

- **Right-sized.** `RateCalculator.Quote` takes a `RateRequest` and returns a
  `decimal` quote in four lines: a base rate off the declared value, a weight
  surcharge, an optional fuel levy on top of those two, and their sum. There
  is one way to rate a consignment today, so there is one static method.
- **Over-abstracted.** The `FreightRating.OverAbstracted` namespace computes
  the identical quote through an `IRateComponent` Strategy interface
  (`BaseRateComponent`, `WeightSurchargeComponent`, `FuelLevyComponent`), an
  `IRateComponentFactory` with its sole implementation
  `StandardRateComponentFactory`, and a `RateEngine` that folds the factory's
  components over a running subtotal. Every seam exists for a second rating
  scheme that has never shown up: one factory implementation, three
  components, one caller.

**What the tests prove.** `RateCalculatorTests` exercises `RateCalculator`
directly: it sums the base rate and weight surcharge, adds the fuel levy only
when it applies, returns zero for an empty request, is pure (the same input
always yields the same output), and exposes exactly one public static method
by reflection, the single entry point the collapsed form promises. The same
file also exercises the tower in isolation, confirming `RateEngine` sums its
components and that `FuelLevyComponent` taxes the running subtotal rather
than the raw request, and that `StandardRateComponentFactory` builds its
three components in base, weight, levy order. `EquivalenceTests` is a
`[Theory]` over a spread of requests, including zero, large values, and the
fuel levy both on and off, asserting `RateCalculator.Quote` and
`new RateEngine(new StandardRateComponentFactory()).Quote` return the exact
same `decimal` for every case: the tower is not wrong, it is just more
machinery than the problem needs.

Shipping domain, no containers: both forms are pure, synchronous
calculations over in-memory values, no network or I/O to fake out.

## Build and test

```sh
cd examples/40-anti-patterns-and-pattern-misuse
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
