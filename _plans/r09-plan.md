---
title: "r09 plan — Part 7 (ch18-20)"
output: false
render_with_liquid: false
---

# r09 — author ch18-20 (Part 7: Composing and adapting structure)

Opus-planned 2026-10-30. Branch `release/r09` (based on main@272a7fa). All three chapters
hands-on; completes Part 7. Four-wave shape (A code / B diagrams AFTER green code with LOCKED
names / C prose / D validate). Enforces: NO cross-language comparison (describe conversion
operators / DispatchProxy / FrozenDictionary positively); keep cross-chapter refs light.

## Scope + example assignments (shipping domain, from Notion map)

- **ch18 Adapter/Facade/Bridge** (`_docs/18-adapter-facade-bridge.md`, Carrier domain). Three
  carrier APIs behind one `ICarrierGateway` (adapters); a `ShipmentBookingFacade` over the
  booking subsystems; a Bridge (`ShipmentLabel` x `ILabelRenderer`); + Money/Weight user-defined
  conversion operators (implicit safe, explicit lossy). Pure BCL.
- **ch19 Decorator/Proxy** (`_docs/19-decorator-and-proxy.md`, Rating domain). Discount->surcharge
  ->tax decorator chain over `BaseRateCalculator`; a `CachingRateGatewayProxy`; DI decoration via
  `AddRatePipeline`; `LoggingRateProxy : DispatchProxy`. MEDI 10.0.14. Synchronous.
- **ch20 Composite/Flyweight** (`_docs/20-composite-and-flyweight.md`, Routing domain).
  Consolidated shipments (`Consolidation`/`ShipmentParcel` under `IConsignmentNode`, recursive
  weight/traversal); flyweight interning of `PortCode`/`CommodityCode` via `FrozenDictionary`
  (reference sharing). Pure BCL.

## Confirmed facts

- Part slug `composing-adapting-structure`; all three stubs carry it. Edit body in place only;
  primary include has `caption="Figure NN.1: TODO caption."` (replace text, keep prefix); each
  stub has `## When not to use it` (AI sidebar there). ch18 stub description already de-Java'd.
- `_data/diagrams.yml` has 32 entries; append from #33.
- Template `examples/11-...`; MEDI idiom `examples/06|17-.../ServiceCollectionExtensions.cs`; MEDI
  pin 10.0.14 (re-survey). `System.Collections.Frozen`, `System.Reflection.DispatchProxy` are in
  the shared framework (no runtime package for ch18/ch20; ch19 pins MEDI only). Keep all synchronous.

## LOCKED type names (Wave A verbatim; Wave B copies from green code)

- **ch18 `examples/18-adapter-facade-bridge/` (CarrierIntegration):** `ShipmentRequest`,
  `CarrierQuote`, `ICarrierGateway.GetQuote`; adaptees `FastlineSoapClient`/`SeabrightRestClient`/
  `AircoLegacyClient` + native DTOs; adapters `FastlineGatewayAdapter`/`SeabrightGatewayAdapter`/
  `AircoGatewayAdapter`; facade `ShipmentBookingFacade(IAddressValidator, ICarrierGateway,
  ICustomsFiler).Book` + `ShipmentConfirmation` + `AddressValidationException` (CA1032);
  Bridge `abstract ShipmentLabel(ILabelRenderer).Render()` + `StandardShipmentLabel`/
  `ReturnShipmentLabel` + `ILabelRenderer`/`PdfLabelRenderer`/`ZplLabelRenderer` + `LabelContent`;
  `Money` (readonly record struct, long Cents, implicit->decimal + ToDecimal, explicit<-decimal +
  FromDecimal); `Kilograms`/`Pounds` (explicit operators + ToPounds/ToKilograms).
- **ch19 `examples/19-decorator-and-proxy/` (RateComposition, MEDI 10.0.14):** `RateRequest`,
  `Money`, `IRateCalculator.Quote`; `BaseRateCalculator`; `DiscountRateDecorator`/
  `SurchargeRateDecorator`/`TaxRateDecorator` (wrap inner; order discount->surcharge->tax);
  `ICarrierRateGateway.GetRate` + `CarrierRate` + `SlowCarrierRateGateway` (call counter) +
  `CachingRateGatewayProxy`; `ServiceCollectionExtensions.AddRatePipeline` (factory wrapping);
  `LoggingRateProxy : DispatchProxy` + `RateProxyFactory.Wrap`.
- **ch20 `examples/20-composite-and-flyweight/` (ConsignmentRouting):** `IConsignmentNode`
  (TotalWeightKilograms, Parcels()); `ShipmentParcel` (record leaf); `Consolidation` (composite,
  recursive); `PortCode` (sealed class) + `PortCodeRegistry` (FrozenDictionary, Get/TryGet,
  `UnknownPortCodeException` CA1032); `CommodityCode` + `CommodityCodeRegistry` (same shape).

## Waves

- **Wave A (code, 3 agents parallel):** build each example green; library csproj with
  `<DefaultItemExcludes>...;<TestDir>/**`; nested `*.Tests/` (4 pins, IsTestProject, IsPackable
  false, NoWarn CA1707, NO TestingPlatformDotnetTestSupport); `.slnx`; README. ch19 pins MEDI
  10.0.14. Capture real path-sanitized output. Tests per acceptance criteria.
- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml):** locked names. #33
  `18-adapter-facade-bridge` + #34 `18-conversion-operators`; #35 `19-decorator-and-proxy` + #36
  `19-di-decoration-and-dispatchproxy`; #37 `20-composite-and-flyweight` + #38
  `20-flyweight-frozen-pool`. Density figures if body >~2500. Real captions; amber.
- **Wave C (prose, 3 agents, after code+figures):** full spine; replace TODO caption; one AI
  sidebar; cite GoF + MS Learn. ch18 operators positive; ch19 Decorator/Proxy/DispatchProxy/DI
  decoration; ch20 Composite/Flyweight/FrozenDictionary. Zero em-dash; 0 ban-tier; idiomatic C#14
  only; NO cross-language; cross-chapter refs light. Wrap `{{`/`}}` fenced blocks in raw.
- **Wave D (validate):** idiomatic-review (0); voice 0 ban-tier (grep editorializing families);
  em-dash 0 across docs + .cs + README; `grep -c "TODO caption"` == 0; diagram name-drift gate;
  NO-CROSS-LANGUAGE gate (docs/code/captions/SVGs for Java|C++|JVM|erasure|Spring == 0);
  CROSS-CHAPTER gate (no "the X chapter"/title pointers/numeric refs); dotnet test per example +
  verify.sh; jekyll build; word counts; log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if >~2500); full spine (figure AFTER
"Why this matters"); figure pair(s) on disk + registered, matching `name`, REAL caption (no TODO);
example `dotnet test` green + in verify.sh; tests assert behaviour; "Build, run, observe" quotes
real path-sanitized output; exactly one AI sidebar in "When not to use it"; zero em-dash (docs +
.cs + README); no rule-of-three; 0 ban-tier; idiomatic C#14 only; NO cross-language; cross-chapter
refs light; no numeric cross-refs; every figure/caption type exists in the example; GoF + MS Learn.
ch18 proves: adapters map native->ICarrierGateway; facade runs the flow; Bridge varies label x
renderer independently; Money->decimal implicit (no cast), decimal->Money explicit (cast) round-trip,
Kilograms<->Pounds explicit round-trip, named alternates match. ch19 proves: decorator chain in
order + composable; caching proxy 2nd-call cached (collaborator Received(1)); DI decoration resolves
wrapped; DispatchProxy intercepts+forwards. ch20 proves: composite sums+traverses nested tree;
flyweight Assert.Same for repeated code from FrozenDictionary pool; distinct differ; unknown throws.
Iteration: jekyll clean; diagrams.yml from #33 in order; test csprojs omit
TestingPlatformDotnetTestSupport + pin 4 packages; each example own `.slnx`; ch18/ch20 no runtime
package; ch19 MEDI 10.0.14; no cross-language token anywhere; Part 7 complete.

## Risks

ch18: CA2225 (named alternates for conversion operators); implicit-vs-explicit (only safe widening
implicit, lossy explicit); CA1021 out-params; CA1051 Bridge fields (ctor + properties). ch19:
DispatchProxy needs an interface + returns object + parameterless ctor with target set after Create;
DI decoration via explicit factory (no Scrutor); decorator order; caching miscount (Received(1));
stay synchronous; MEDI pin drift. ch20: FrozenDictionary via ToFrozenDictionary + using
System.Collections.Frozen; flyweight must be `sealed class` not record (value equality defeats the
point); CA1002 (expose IReadOnlyList); deep recursion (shallow test trees); CA1032. All: diagram
name drift (diagrams after code + gate); TODO-caption trap; no-cross-language slip (tempting at ch18
operators + ch19 DispatchProxy); heavy cross-chapter refs; Liquid `{{ }}`; .slnx/global.json; CA1707
test-only; path leaks; word floor.

## r09 verification log

_(filled in Wave D)_
