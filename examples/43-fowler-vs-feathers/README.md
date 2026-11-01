# 43-fowler-vs-feathers

A legacy, untestable rate engine sits next to two refactored forms over the
same shipping domain: a minimally-seamed form that applies only Michael
Feathers' seam idea, and a fully refactored form that goes on to apply Martin
Fowler's "Extract and Override Call" lineage, extracting a polymorphic
surcharge strategy and the shared rate rules.

- **Legacy, untestable.** `RateEngineCatalogues.Legacy.LegacyRateEngine` is
  `sealed`, has a single parameterless constructor, and its `Quote` method is
  non-virtual. It builds its own `LegacyTariffTable` with a hard-coded `new`
  instead of receiving one, reads the peak-hour surcharge through the ambient
  `DateTime.Now` instead of an injected clock, and selects the service
  surcharge with an inline string `switch` instead of a strategy. Nothing
  about it can be substituted from outside the class.
- **Feathers stage, minimally seamed.** `RateEngineCatalogues.SeamedRateEngine`
  takes an `ITariffSource` and an `IClock` through its primary constructor, so
  a test can substitute both, and nothing else about the method moves: the
  loose `(decimal, decimal, string)` signature, the inline string `switch`,
  and the inline rate literals all stay exactly as the legacy shape had them.
  This is the smallest change that opens a seam.
- **Fowler stage, fully refactored.** `RateEngineCatalogues.RateEngine` keeps
  the same two constructor-injected collaborators, but replaces the inline
  `switch` with the `IServiceSurcharge` strategy `ServiceSurcharges` selects
  (`StandardSurcharge`, `ExpressSurcharge`, `OvernightSurcharge`), replaces the
  inline rate literals with the named `RateRules` constants, and replaces the
  loose parameter list with a single `RateRequest` that carries a typed
  `ServiceLevel`. `StandardTariffSource` and `SystemClock` are the production
  implementations of the two interfaces shared by both refactored forms, and
  `PeakWindow.IsPeak` is the `internal` peak-hour rule both delegate to.

**What the tests prove.** `FowlerFeathersTests` quotes the fully refactored
engine with a `FakeClock` fixed outside and inside the peak window, asserts
the exact deterministic amounts for the standard, express, and overnight
surcharges, and checks the quote is stamped with the clock's own instant. An
NSubstitute substitute for `ITariffSource` shows the tariff collaborator is
substitutable independent of the clock. A `[Theory]` over `ServiceSurcharges`
proves the strategy lookup is correct for every `ServiceLevel`, and a second
`[Theory]` reaches `PeakWindow.IsPeak` and the `RateRules` constants directly
through `InternalsVisibleTo`. `EquivalenceTests` proves the Feathers-seamed
`SeamedRateEngine` and the Fowler-refactored `RateEngine` compute the
identical amount for the same request, the same `StandardTariffSource`, and
the same fixed clock instant, across every service level and both inside and
outside the peak window: the deeper refactor changes the method's shape, never
its behavior. `LegacyContrastTests` proves the legacy engine's untestability
by structure, through reflection (sealed, one parameterless constructor,
non-virtual `Quote`), not by asserting on the wall clock, which would be
flaky. `StandardTariffSource` is also checked directly against the same rate
the legacy table seeds, so the contrast between all three forms is over
identical data.

Shipping domain, no containers: all three engines are pure, synchronous rate
calculations over in-memory state, no network or I/O to fake out.

## Build and test

```sh
cd examples/43-fowler-vs-feathers
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
