---
title: "r06 plan — Part 5 (ch11-12)"
output: false
render_with_liquid: false
---

# r06 — author ch11-12 (Part 5: Selecting and varying behaviour)

Opus-planned 2026-10-29. Branch `release/r06` (based on main@791eef9). Both chapters
hands-on. Mirrors the r04/r05 four-wave shape, scaled to two examples. ch13-14 (State,
Command) are r07, OUT of scope here.

## Scope

- **ch11 Strategy** (`_docs/11-strategy-delegates-interfaces-keyed-services.md`, Rating
  domain). Rate quotes priced three ways by the SAME strategy abstraction at three levels:
  (1) a `Func<>` delegate, (2) a named `interface`, (3) a KEYED SERVICE resolved from MEDI
  by key. A "Tier 1 dissolve": GoF Strategy -> delegate -> interface when it earns its
  place -> keyed DI. Keyed services are blog-only research, so BACK every claim with the
  runnable example AND cite MS Learn DI docs.
- **ch12 Template Method** (`_docs/12-template-method-and-the-delegate-alternative.md`,
  Documentation domain). A customs declaration whose skeleton is fixed but whose steps vary
  by destination country. Show BOTH the classic abstract-base/protected-virtual-steps form
  AND the modern delegate-record alternative; both modern idiomatic C# (no bad "before").
  GoF-backed.

## Confirmed facts

- Both stubs exist with a PRIMARY-figure include carrying `caption="Figure 11.1/12.1: TODO
  caption."`. Edit body in place only. Keep the `Figure N.1:` prefix; replace ONLY the
  `TODO caption.` text with a real caption (explicit Wave C deliverable + Wave D grep gate).
- `_data/diagrams.yml` has 18 entries; append from #19 in chapter order.
- Amber accent (`#e8870c`/`#b8650a`/`#fff8ef`/`#111111`); emphasis boxes light fill + dark text.
- Example template = `examples/06-isp-dip-and-built-in-di/` + `examples/10-...`. MEDI pin
  re-surveyed 2026-10-29: latest 10.0.x = **10.0.14** (same as ch6). Keep pricing/declaration
  SYNCHRONOUS (pure computation) to sidestep CA2007/xUnit1051.
- Both stubs HAVE a `## When not to use it` section; each AI sidebar lives there.

## Waves

- **Wave A (code, parallel):**
  - `examples/11-strategy-delegates-interfaces-keyed-services/` (project `RateQuoting`):
    `Money`/`RateRequest`/`RateQuote` records; `IPricingStrategy.Price(RateRequest)`;
    `StandardTariff`/`ExpressTariff`/`EconomyTariff`; `DelegateRateQuoter` (Func<> level),
    `StrategyRateQuoter` (interface level), `KeyedTariffQuoter` (keyed-DI level, resolves by
    `request.ServiceLevel` via `GetRequiredKeyedService`) + a `[FromKeyedServices]` consumer;
    `ServiceCollectionExtensions` with `AddKeyedSingleton` x3. MEDI 10.0.14 in the library.
    Tests: delegate selects/ swaps pricing (assert on Money, never delegate identity);
    interface swap; keyed resolve by key, `[FromKeyedServices]`, unknown-key throws /
    `GetKeyedService` null, non-keyed `GetService` returns null (separate graphs trap).
  - `examples/12-template-method-and-the-delegate-alternative/` (project
    `CustomsDocumentation`): `Shipment`/`Money`/`CustomsDeclaration` records; inheritance form
    `CustomsDeclarationBuilder` (abstract, protected ctor, SEALED non-virtual `Build`
    skeleton, protected abstract/virtual steps CollectRequiredFields/CalculateDuty/Validate)
    + `UnitedStates`/`Germany`/`Japan` subclasses; delegate form `DeclarationSteps` (record of
    Func<>) + `DeclarationEngine.Build(Shipment, DeclarationSteps)` + per-country step-sets.
    Tests: step order (both forms); per-country declarations differ via same skeleton;
    inheritance vs delegate parity (record equality) for a country.
  - Each: library csproj with `<DefaultItemExcludes>...;<TestDir>/**`, nested `*.Tests/`
    (xunit.v3 4.0.2 / runner 4.0.1 / Test.Sdk 18.11.0 / NSubstitute 6.2.0; IsTestProject,
    IsPackable false, NoWarn CA1707; NO TestingPlatformDotnetTestSupport), a `.slnx`, README.
    Capture real path-sanitized `dotnet test` output.
- **Wave B (diagrams):** one agent owns diagrams.yml. #19 `11-strategy-delegates-interfaces-
  keyed-services` (strategy at three levels, one caller unchanged); #21
  `12-template-method-and-the-delegate-alternative` (fixed skeleton + varying steps,
  inheritance alongside delegate-record). Density figures #20 `11-keyed-service-resolution`
  and #22 `12-template-method-step-order` ONLY if the respective body passes ~2500 words.
  Every figure (primary included) gets a REAL caption.
- **Wave C (prose):** write each after its example is green. ch11: delegate -> interface ->
  keyed services (`AddKeyedSingleton/Scoped/Transient`, `GetRequiredKeyedService` vs
  `GetKeyedService`, `[FromKeyedServices]`, keyed-invisible-to-non-keyed trap), quoting real
  tests; cite MS Learn DI/keyed-services. ch12: inheritance Template Method + delegate
  alternative + where inheritance earns its place; cite GoF. Both: full spine, one AI sidebar
  in "When not to use it", REPLACE the primary-figure TODO caption, zero em-dash, idiomatic
  C# 14 only, no numeric cross-refs. Wrap fenced blocks containing `{{` in raw.
- **Wave D (validate):** idiomatic-review both examples; voice to 0 ban-tier; em-dash scan = 0
  across both `.md` AND examples' `.cs` AND README; `dotnet test` per example + verify.sh;
  jekyll build; word counts; grep `TODO caption` == 0 in both docs; log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target, no ceiling; density figure if >~2500); full
spine (figure AFTER "Why this matters"); figure pair(s) on disk + registered with matching
`name` and a real caption (no TODO); example `dotnet test` green + in verify.sh; tests assert
behaviour; "Build, run, observe" quotes real path-sanitized output; exactly one AI sidebar in
"When not to use it"; zero em-dash (docs + .cs + README); no rule-of-three; idiomatic C# 14
only, idiomatic-review clean; no intra-book numeric cross-refs.
ch11 proves: Func<> strategy selects/swaps with caller unchanged; interface swap; keyed
resolve by key, `[FromKeyedServices]`, unknown-key throws / GetKeyedService null, non-keyed
GetService null. Cites MS Learn DI/keyed-services. ch12 proves: fixed step order (both forms);
per-country declarations differ via same skeleton; inheritance/delegate parity. Cites GoF.
Iteration: jekyll build clean; diagrams.yml appended from #19 in order; new test csprojs omit
TestingPlatformDotnetTestSupport; each example has its own `.slnx`; ch11 pins MEDI 10.0.14.

## Risks

ch11 keyed-services API correctness (`AddKeyedSingleton`/`[FromKeyedServices]`/
`GetRequiredKeyedService` vs `GetKeyedService`; keyed and non-keyed are separate graphs);
MEDI pin drift; delegate capture/equality (assert on Money not Func identity). ch12 abstract
base analyzers (CA1012 protected ctor, CA1051 no public fields, CA1052, seal the template
method); delegate-record `Func<>`/collection-expression `{{` in fenced blocks (wrap in raw).
Both: Liquid `{{ }}`/`{% %}`; .slnx/global.json; xUnit1051/CA2007 (kept sync); CA1707; path
leaks; word floor; diagram name drift; TODO-caption trap on primary figures (Wave D grep gate).

## r06 verification log

_(filled in Wave D)_
