# 41-seams-non-virtual-by-default

A legacy, untestable rate engine sits next to a refactored, seam-enabled one
over the same shipping domain, showing what Michael Feathers calls a "seam"
against C#'s default of non-virtual methods and sealed types.

- **Legacy, untestable.** `RateEngineSeams.Legacy.RateEngine` is `sealed`,
  has a single parameterless constructor, and its `Quote` method is
  non-virtual. It builds its own `LegacyTariffTable` with a hard-coded `new`
  instead of receiving one, and reads the peak-hour surcharge through the
  ambient `DateTime.Now` instead of an injected clock. Nothing about either
  collaborator can be substituted from outside the class: there is no
  constructor seam, no interface seam, and no override seam.
- **Refactored, seam-enabled.** `RateEngineSeams.RateEngine` takes an
  `ITariffSource` and an `IClock` through its primary constructor, so a test
  can substitute both. The peak-hour surcharge is also its own
  extract-and-override seam: `PeakSurcharge` is `protected virtual`, so a test
  subclass can replace the surcharge rule entirely without touching `Quote`.
  `StandardTariffSource` and `SystemClock` are the production implementations
  of the two interfaces; `PeakWindow.IsPeak` is the `internal` peak-hour rule
  the default `PeakSurcharge` delegates to.

**What the tests prove.** `SeamTests` quotes the refactored engine with a
`FakeClock` fixed outside the peak window and asserts the exact deterministic
amount, then fixes the clock inside the window and asserts the surcharge is
applied, and separately asserts the quote is stamped with the clock's own
instant. An NSubstitute substitute for `ITariffSource` shows the tariff
collaborator is substitutable independent of the clock, and
`TestableRateEngine`, a test subclass overriding `PeakSurcharge`, shows the
extract-and-override seam replacing the surcharge rule outright. A `[Theory]`
reaches `PeakWindow.IsPeak` directly across the peak-hour boundary through
`InternalsVisibleTo`. `LegacyContrastTests` proves the legacy engine's
untestability by structure, through reflection (sealed, one parameterless
constructor, non-virtual `Quote`), not by asserting on the wall clock, which
would be flaky. `StandardTariffSource` is also checked directly against the
same rate the legacy table seeds, so the contrast between the two forms is
over identical data.

Shipping domain, no containers: both engines are pure, synchronous rate
calculations over in-memory state, no network or I/O to fake out.

## Build and test

```sh
cd examples/41-seams-non-virtual-by-default
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
