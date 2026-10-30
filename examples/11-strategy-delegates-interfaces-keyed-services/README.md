# 11-strategy-delegates-interfaces-keyed-services

Strategy dissolved across three levels of ceremony, all pricing the same
`RateRequest` and returning the same `RateQuote`:

- **Delegate (Tier 1).** `DelegateRateQuoter` takes a plain
  `Func<RateRequest, Money>`. There is no `IPricingStrategy` at all; swapping
  the pricing algorithm is passing a different lambda to the constructor.
- **Interface (Tier 2).** `StrategyRateQuoter` takes a constructor-injected
  `IPricingStrategy`, the textbook GoF shape. `StandardTariff`, `ExpressTariff`,
  and `EconomyTariff` price the same `RateRequest` differently: a flat
  per-kilogram rate, a per-kilogram surcharge over it, and a per-kilogram
  discount under it.
- **Keyed service (Tier 3).** `KeyedTariffQuoter` resolves the matching
  `IPricingStrategy` from the built-in DI container by
  `RateRequest.ServiceLevel` ("standard", "express", "economy") using
  `GetRequiredKeyedService`, instead of branching on that string itself.
  `ExpressLaneQuoter` shows the other half of keyed services: a constructor
  parameter attributed `[FromKeyedServices("express")]` that always resolves
  the express tariff, chosen once at construction rather than per call.
  `ServiceCollectionExtensions.AddRateQuoting` is the one place that lists
  which tariff backs each key.

**What the tests prove.** `DelegateRateQuoterTests` prices a request through
one delegate, then shows a different closure (a surcharge, then a discount)
changes the quote from the same `DelegateRateQuoter` type. `StrategyRateQuoterTests`
builds `StrategyRateQuoter` against `StandardTariff` and `ExpressTariff` and
asserts the resolved prices differ while the caller class never changes.
`KeyedTariffQuoterTests` builds a real `ServiceProvider` from
`AddRateQuoting()` and asserts: `GetRequiredKeyedService<IPricingStrategy>("express")`
returns an `ExpressTariff` and prices accordingly; `KeyedTariffQuoter` dispatches
correctly across all three registered keys; `ExpressLaneQuoter` resolves its
fixed `[FromKeyedServices("express")]` dependency; an unknown key makes
`GetRequiredKeyedService` throw while `GetKeyedService` returns null; a
non-keyed `GetService<IPricingStrategy>()` returns null because keyed and
non-keyed registrations are separate graphs; and swapping which key
`KeyedTariffQuoter` is asked for changes the quote without editing that class.

Shipping domain, no containers: `Money` is a validating value type, and the
three tariffs are pure, synchronous pricing computations, no network or I/O
to fake out.

## Build and test

```sh
cd examples/11-strategy-delegates-interfaces-keyed-services
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
