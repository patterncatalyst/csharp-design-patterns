# 44-refactor-or-rewrite

A legacy, untestable rate engine sits next to two replacements over the same
shipping-domain contract: a refactor-in-place form and a clean rewrite,
showing the "refactor or rewrite" decision applied to C#.

- **Legacy, no stable seam.** `RateEngineReplacement.Legacy.LegacyRateEngine`
  is `sealed`, has a single parameterless constructor, its `Quote` method is
  non-virtual, and it does not implement `IRateEngine`. It builds its own
  `LegacyTariffTable` with a hard-coded `new` instead of receiving one, and
  reads the peak-hour surcharge through the ambient `DateTime.Now` instead of
  an injected clock. Nothing about either collaborator can be substituted
  from outside the class, and no caller can hold it behind a contract that
  either replacement could also satisfy.
- **Refactor in place.** `RefactoredRateEngine` moves the legacy calculation
  behind `IRateEngine` one step at a time: the tariff table and the clock
  become constructor-injected collaborators, but the original shape of the
  computation, a running subtotal built up statement by statement, is left
  intact.
- **Clean rewrite.** `RewrittenRateEngine` satisfies the identical `IRateEngine`
  contract, but as one consolidated expression instead of a running subtotal.
  It is exercised against the same inputs and the same fixed clock as the
  refactored engine to prove the rewrite is not a silent behavior change.
- **Bound to the seam.** `QuoteService` depends only on `IRateEngine`, so
  swapping the refactored engine for the rewritten one, or back again, is a
  one-line change at the construction site and no change at all inside
  `QuoteService` itself.

**What the tests prove.** `RefactorOrRewriteTests` quotes both the refactored
and the rewritten engine with a `FakeClock` fixed outside the peak window and
asserts the exact deterministic amount, then fixes the clock inside the
window and asserts the surcharge is applied to both, runs `QuoteService`
against each engine through identical caller code, and substitutes an
NSubstitute `ITariffSource` into both engines through a `[Theory]` over a
factory list to show the tariff collaborator is substitutable independent of
which replacement is in play. A `[Theory]` reaches `PeakWindow.IsPeak`
directly across the peak-hour boundary through `InternalsVisibleTo`.
`EquivalenceTests` proves the two replacements compute the identical
`RateQuote` for identical input, the same `StandardTariffSource`, and the
same fixed clock, checked through record value equality across a scenario
set spanning both sides of the peak window and more than one lane.
`LegacyContrastTests` proves the legacy engine has no stable seam to refactor
or rewrite behind, through reflection (sealed, one parameterless constructor,
non-virtual `Quote`, not assignable to `IRateEngine`), not by asserting on the
wall clock, which would be flaky.

Shipping domain, no containers: all three engines are pure, synchronous rate
calculations over in-memory state, no network or I/O to fake out.

## Build and test

```sh
cd examples/44-refactor-or-rewrite
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
