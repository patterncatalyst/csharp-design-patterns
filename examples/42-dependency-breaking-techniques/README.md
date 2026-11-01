# 42-dependency-breaking-techniques

The same rate-quoting engine, shown twice, to make dependency-breaking
techniques concrete over one shipping scenario.

- **Legacy, baked in.** `RateEngineDependencies.Legacy.RateEngine.Quote` news
  up a `LegacyAuditLog`, reaches the static `LegacyTariffBook`, and reads
  `Guid.NewGuid()`, `DateTime.UtcNow`, and `Environment.UserName` directly.
  There is no constructor to inject a fake into and no virtual seam to
  override, so nothing about a quote (which tariff, what time, which
  reference, who quoted it) can be pinned down from a test. It still
  compiles clean under the same analyzers and warnings-as-errors as every
  other example in this book; the untestability is structural, not a build
  failure.
- **Refactored, dependency-broken.** `RateEngineDependencies.RateEngine`
  breaks each of those dependencies through a distinct technique:
  - **Interface extraction** turns the static `LegacyTariffBook` into an
    injected `ITariffBook`, and the self-constructed `LegacyAuditLog` into an
    injected `IAuditLog`.
  - **Constructor injection** (a C# 14 primary constructor) wires in
    `ITariffBook`, `IAuditLog`, `IClock`, and `IReferenceGenerator`, replacing
    `DateTime.UtcNow` and `Guid.NewGuid()` with seams a test controls.
  - **Parameterize Method** replaces `Environment.UserName` with a
    `quotedBy` parameter on `Quote`, so the caller's identity is an input,
    not an ambient read.
  - **Extract-and-Override** gives `NewReference()` a `protected virtual`
    seam of its own: a subclass can replace reference generation directly,
    without an `IReferenceGenerator` to inject, which is exactly what the
    test double `FixedReferenceEngine` does.
  - **InternalsVisibleTo** exposes `LaneValidation`, an `internal` lane-code
    check, to the test project directly, without widening it to a public
    member of `RateEngine`.

**What the tests prove.** `DependencyBreakingTests` drives the refactored
engine through NSubstitute fakes for all four injected interfaces: the
quoted amount comes from whatever `ITariffBook` returns, `QuotedAt` comes
from the fake `IClock`, the reference comes from the fake
`IReferenceGenerator`, and `QuotedBy` carries the parameter the caller
passed, not the process's real user. A separate case proves
`FixedReferenceEngine`'s override wins over an injected generator that would
otherwise return a different reference, and a theory reaches
`LaneValidation.IsKnownLane` straight through `InternalsVisibleTo` across
known and unknown lanes, including the case `Quote` turns into an
`UnknownLaneException`. `LegacyContrastTests` proves the legacy engine's
untestability by reflection, not by a flaky assertion on an ambient value:
the type is sealed with only a parameterless constructor, `Quote` is a
non-virtual method, and `LegacyTariffBook` is the CLR's abstract-sealed
shape for a static class. A final case runs the production `StandardTariffBook`
and `GuidReferenceGenerator` directly, checking the real rates and the shape
of a real `Guid` reference (32 hex characters, no separators) without
asserting a specific value.

Shipping domain, no containers: both engines are pure, synchronous
collaborations, no network or I/O to fake out. The legacy form never
prints, logs, or persists its `Environment.UserName` read; it only flows
into the in-memory `RateQuote` the method returns.

## Build and test

```sh
cd examples/42-dependency-breaking-techniques
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
