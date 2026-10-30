---
title: "r08 plan — Part 6 (ch15-17)"
output: false
render_with_liquid: false
---

# r08 — author ch15-17 (Part 6: Creating objects under control)

Opus-planned 2026-10-30. Branch `release/r08` (based on main@9c54365). All three chapters
hands-on; completes Part 6. Mirrors r06/r07 four-wave shape, with diagrams generated AFTER
green code using LOCKED names (r06 name-drift lesson). Enforces the new rules: NO
cross-language comparison (reified generics described positively), and keep cross-chapter
references light (chapters stand alone).

## Scope + example assignments (shipping domain, from Notion map)

- **ch15 Factory Method + Abstract Factory** (`_docs/15-factory-method-and-abstract-factory.md`,
  Carrier domain). A gateway-per-carrier resolved BY TYPE via reified generics: `typeof(TCarrier)`
  type-keyed registry, `where TCarrier : ICarrier` constraints, `static abstract` carrier `Code`.
  Factory Method (overridable creator + delegate alt) + Abstract Factory (per-carrier family:
  gateway + request builder + response parser). Pure BCL. Describe reification POSITIVELY.
- **ch16 Builder + required members + collection expressions; Prototype + with**
  (`_docs/16-builder-required-members-prototype-with.md`, booking domain). required-members +
  object/collection initializers often remove the need for a Builder; a validating Builder where
  it still fits; Prototype via `with` on a template record (shallow-copy caveat). Pure BCL.
- **ch17 Singleton + Lazy<T> + DI lifetimes** (`_docs/17-singleton-lazy-and-di-lifetimes.md`,
  tariff-table domain). Tariff table three ways: classic static singleton (`beforefieldinit`
  note), `Lazy<T>` (ExecutionAndPublication, counting factory proves single init), MEDI
  AddSingleton vs AddScoped/AddTransient + captive-dependency/ValidateScopes. MEDI 10.0.14.

## Confirmed facts

- Part slug `creating-objects-under-control`; all three stubs carry it. Edit body in place only;
  primary include has `caption="Figure NN.1: TODO caption."` (replace text, keep prefix); each
  stub has `## When not to use it` (AI sidebar lives there).
- `_data/diagrams.yml` has 26 entries (last `14-async-command-undo`); append from #27.
- MEDI re-surveyed 2026-10-30 = 10.0.14. ch15/ch16 pin NO runtime package.
- Example template = `examples/11-...`; DI precedent `examples/06-.../ServiceCollectionExtensions.cs`.
  Keep all three SYNCHRONOUS (no CA2007/xUnit1051).

## LOCKED type names (Wave A verbatim; Wave B copies from green code)

- **ch15 `examples/15-factory-method-and-abstract-factory/` (CarrierProvisioning):** records
  `ShipmentRequest`/`CarrierRequest`/`ShipmentConfirmation`; `ICarrier{ static abstract string Code }`
  + markers `Fastline`/`Seabright`/`Airco`; products `ICarrierGateway`/`IShipmentRequestBuilder`/
  `IShipmentResponseParser` + per-carrier concretes; `ICarrierGatewayFactory<TCarrier> where
  TCarrier:ICarrier` (CreateGateway/CreateRequestBuilder/CreateParser) + concretes; `CarrierRegistry`
  (Register<TCarrier>/FactoryFor<TCarrier> keyed by typeof(TCarrier)); Factory-Method `CarrierDispatcher`
  (protected abstract CreateGateway, Dispatch) + per-carrier subclasses + `DelegateCarrierDispatcher
  (Func<ICarrierGateway>)`; `ShipmentDispatchService`; `UnknownCarrierException` (CA1032 3 ctors).
- **ch16 `examples/16-builder-required-members-prototype-with/` (BookingRequests):** `Money`
  (readonly record struct, Of); `LineItem(Description, Money, Quantity)`; `BookingRequest` record
  (required Origin/Destination/DeclaredValue; init ServiceLevel="standard"/LineItems=[]/InsuranceRequested/
  SpecialInstructions?/CustomerReference?); fluent `BookingRequestBuilder` (With*/AddLineItem/Build
  validates -> BookingValidationException); `BookingTemplates.StandardDomestic` prototype;
  `BookingValidationException` (CA1032 3 ctors).
- **ch17 `examples/17-singleton-lazy-and-di-lifetimes/` (TariffTables, MEDI 10.0.14):**
  `TariffEntry(Origin, Destination, RatePerKilogram)`; `ITariffSource{ IReadOnlyList<TariffEntry>
  Load(); int LoadCount }`; `CountingTariffSource`; `TariffTable(ITariffSource)` with Lookup;
  `LazyTariffTable` (Lazy<TariffTable>, ExecutionAndPublication, Table triggers load);
  `ClassicTariffTable` (static singleton, private ctor, Instance, explicit static ctor);
  `ServiceCollectionExtensions.AddTariffTables` (AddSingleton) + Scoped/Transient contrast helpers.

## Waves

- **Wave A (code, 3 agents parallel):** build each example green; library csproj with
  `<DefaultItemExcludes>...;<TestDir>/**`; nested `*.Tests/` (4 pins, IsTestProject, IsPackable
  false, NoWarn CA1707, NO TestingPlatformDotnetTestSupport); `.slnx`; README. ch17 pins MEDI
  10.0.14. Capture real path-sanitized output. Tests per the acceptance criteria below.
- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml):** locked names copied from
  code. #27 `15-factory-method-and-abstract-factory` + #28 `15-type-keyed-carrier-registry`;
  #29 `16-builder-required-members-prototype-with` + #30 `16-with-prototype-shallow-copy`;
  #31 `17-singleton-lazy-and-di-lifetimes` + #32 `17-di-lifetime-resolution`. Density figures
  only if the body passes ~2500 words. Real captions; amber; light-fill emphasis boxes.
- **Wave C (prose, 3 agents, after each example green + figures):** full spine; replace TODO
  caption; one AI sidebar in "When not to use it"; cite GoF + MS Learn. ch15 reified generics
  POSITIVE (no Java/erasure). ch16 required-members/initializers vs Builder + `with` shallow-copy.
  ch17 Singleton-as-DI-lifetime + Lazy<T> + lifetimes + captive dependency. Zero em-dash; 0
  ban-tier (incl. editorializing families + kickers); idiomatic C# 14 only; NO cross-language
  comparison; **keep cross-chapter references light (name the pattern in place, do not lean on
  other chapters by number OR title)**. Wrap `{{`/`}}`-adjacent fenced blocks in raw.
- **Wave D (validate):** idiomatic-review (0); voice 0 ban-tier (grep earns-its-place/for-free/
  genuinely/honest/kickers); em-dash 0 across docs + .cs + README; `grep -c "TODO caption"` == 0;
  diagram name-drift gate (every SVG/caption token exists in the example); NO-CROSS-LANGUAGE gate
  (grep docs/code/captions/SVGs for Java|C++|JVM|erasure|Spring == 0); CROSS-CHAPTER gate (grep
  docs for "the X chapter"/"chapter on"/heavy title pointers); dotnet test per example + verify.sh;
  jekyll build; word counts; log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target, no ceiling; density figure if >~2500); full spine
(figure AFTER "Why this matters"); figure pair(s) on disk + registered, matching `name`, REAL
caption (no TODO); example `dotnet test` green + in verify.sh; tests assert behaviour; "Build,
run, observe" quotes real path-sanitized output; exactly one AI sidebar in "When not to use it";
zero em-dash (docs + .cs + README); no rule-of-three; 0 ban-tier; idiomatic C# 14 only; NO
cross-language comparison; cross-chapter refs light; no intra-book numeric cross-refs; every
figure/caption type exists in the example.
ch15 proves: right family per typeof(TCarrier); new carrier by registration without editing the
registry; unknown throws; Factory-Method/delegate parity; end-to-end dispatch. ch16 proves: valid
init construction; `with` clones+amends without mutating; collection-spread shallow-copy; Builder
== initializer (record equality); builder validation throws. ch17 proves: Lazy single init (0
before, 1 after, 1 under parallel); DI singleton same instance / transient+scoped differ; captive
dependency fails ValidateScopes; classic Instance stable.
Iteration: jekyll build clean; diagrams.yml from #27 in order; new test csprojs omit
TestingPlatformDotnetTestSupport + pin 4 packages; each example own `.slnx`; ch15/16 no runtime
package; ch17 MEDI 10.0.14; no cross-language token anywhere; Part 6 complete.

## Risks

ch15: CA1040 empty marker (give ICarrier `static abstract string Code`); reified-generics cast
correctness; no-cross-language slip at the erasure-contrast temptation (grep gate). ch16: Liquid
`{{`/`}}` in nested initializers / `with { LineItems = [..] }` (wrap raw); record `with` shallow
copy (explicit); Builder/initializer equality; CS9035 compile-time only (mirror with builder
guard). ch17: LazyThreadSafetyMode.ExecutionAndPublication; static singleton beforefieldinit;
MEDI captive dependency / ValidateScopes; MEDI pin drift. All: diagram name drift (diagrams after
code + gate); TODO-caption trap; .slnx/global.json MTP; CA1707 test-only; path leaks; word floor;
CA1032 3 ctors on the two custom exceptions.

## r08 verification log

Opus-validated 2026-10-30 (branch `release/r08`). Overall: PASS, no must-fixes, no repair round.

**Build / test (all green):**
- `examples/15-factory-method-and-abstract-factory` `dotnet test`: 14/14.
- `examples/16-builder-required-members-prototype-with` `dotnet test`: 5/5.
- `examples/17-singleton-lazy-and-di-lifetimes` `dotnet test`: 15/15.
- `scripts/verify.sh`: all three new examples auto-discovered and green alongside the rest.
- `bundle exec jekyll build`: clean, all six r08 figures render.

**Content:** all three chapters full spine in order (figure after "Why this matters"); "Build,
run, observe" quotes real counts (14/5/15), path-sanitized (0 `/home/`, 0 username); exactly one
AI sidebar each in "When not to use it"; body prose 3107 (ch15) / 3578 (ch16) / 3512 (ch17),
each with a density figure; 0 em-dash; citations learn.microsoft.com + GoF (pearson).

**NO CROSS-LANGUAGE (highest-risk = ch15 reified generics):** grep for Java/JVM/erasure/C++/Spring
across all three docs, all three example trees (.cs + README), the six new diagrams.yml captions,
and all six SVGs = ZERO. Reification described positively ("a generic type argument in C# is
available at runtime").

**CROSS-CHAPTER REFS LIGHT (new rule):** no intra-book numeric cross-refs; all "chapter" hits are
self-referential ("this chapter") or within-chapter section pointers. No pointer to another
chapter by number or title.

**Diagrams (name-accuracy gate):** six figures (#27-32, chapter order) each paired on disk,
registered, included in the right chapter. Every SVG label + caption token verified present in the
example source. No drift. (Generated after green code with locked names.)

**Code idiom:** `lgtm-idiomatic-review` 0 hits on all three. ch15: ICarrier static-abstract Code
(CA1040 handled), typeof(TCarrier) key + where-constraints + registered factory (no Activator).
ch16: required members + records + `with` + collection-spread, no "before" code. ch17: Lazy<T>
ExecutionAndPublication, static singleton + explicit static ctor, MEDI lifetimes + captive-
dependency via ValidateScopes. All test csprojs set IsTestProject + IsPackable false + NoWarn
CA1707, pin the four packages, omit TestingPlatformDotnetTestSupport; each example own `.slnx`;
ch15/16 no runtime package; ch17 MEDI 10.0.14; UnknownCarrierException + BookingValidationException
have CA1032 three ctors. Em-dash across all three examples' .cs + README = 0.

**Voice:** `lgtm-professional-voice` 0 ban-tier on all three (watch-tier only). Editorializing
family grep clean.

**Part 6 is complete** (ch15-17). No repairs were needed this iteration.
