---
title: "r19 plan — Part 14 (ch45, ch46)"
output: false
render_with_liquid: false
---

# r19 — author ch45, ch46 (Part 14: Putting It Together)

Opus-planned 2026-11-01. Branch `release/r19` (based on main@09275e5, r18 merge).
The integration part: ch45 an end-to-end shipping slice weaving several patterns,
ch46 reviewing pattern-shaped LLM output with a Roslyn analyzer as the enforcement
gate. Four-wave relay. "Capstone" is BANNED — ch45 is the "Final Project".

## Structural asymmetry (both are NON-pattern chapters)
- Neither gets `## When not to use it` (that is a single-pattern-chapter section).
- **ch45**: hands-on (keeps `## Build, run, observe`); closes with `## What the
  slice shows` holding **exactly ONE** `<blockquote class="ai-sidebar" markdown="1">`
  (the Final Project is where a reader leans on an assistant). Wave D gate: ch45
  AI-sidebar `grep -c == 1`.
- **ch46**: hands-on (stub LACKS `## Build, run, observe` — Wave C ADDS it); it IS
  the AI chapter, so a separate AI-sidebar is redundant → **NO AI-sidebar**. Wave D
  gate: ch46 AI-sidebar `grep -c == 0`.
- Primary include names LOCKED: `45-final-project`, `46-working-with-ai-and-llm-tools`.
- diagrams.yml = 88; append from **#90** chapter order (ch45 #90-92, ch46 #93-95) → 94.
- No new bibliography.html entry needed (GoF 1994 + Fowler 2018 already cover it).
  "For deeper coverage" = prose pointers + Microsoft Learn (Roslyn-analyzer mechanics
  for ch46; DI/records/pattern-matching for ch45).
- Notion source says "Capstone" + carries cross-language notes — TRANSLATE/STRIP
  (planning notes only, never into chapters).

## LOCKED type names (Wave A builds verbatim)

### ch45 `examples/45-final-project/` — ns `ShipmentWorkflow`, pure BCL, no MEDI
Project `ShipmentWorkflow.csproj`; tests `ShipmentWorkflow.Tests`; `.slnx`; README.
csproj: no runtime PackageReference; `DefaultItemExcludes` for `ShipmentWorkflow.Tests/**`;
`<InternalsVisibleTo Include="ShipmentWorkflow.Tests" />`.

Shared domain (root):
- `public enum ServiceLevel { Standard, Express }`
- `public sealed record BookingRequest(string ConsignmentId, string OriginPort, string DestinationPort, decimal DeclaredValue, decimal WeightKilograms, ServiceLevel Service);`
- `public sealed record RateQuote(string ConsignmentId, decimal Amount);`
- `public sealed record StowageAssignment(string ConsignmentId, string SlotId);`
- `public sealed record LineItem(string Description, decimal Amount);`
- `public sealed record Invoice(string ConsignmentId, IReadOnlyList<LineItem> LineItems, decimal Total);`
- `public sealed record ClearanceResult(bool Cleared, string? Reason)` + static `Clear()` / `Rejected(string reason)`.
- `internal static class RatingMath { DeclaredValueRate=0.015m; PerKilogram=2.50m; ExpressMultiplier=1.40m; }`

Strategy + Factory (root):
- `public interface IRatingStrategy { RateQuote Quote(BookingRequest request); }`
- `StandardRatingStrategy` (DeclaredValue*0.015m + WeightKilograms*2.50m), `ExpressRatingStrategy` (standard base * 1.40m).
- `public static class RatingStrategies { public static IRatingStrategy For(ServiceLevel service); }`

Stowage: `public interface ISlotAllocator { string Allocate(string consignmentId); }` + `SequentialSlotAllocator` ("SLOT-001"...).
Clearance: `public interface IClearanceCheck { ClearanceResult Check(BookingRequest request); }` + `StandardClearanceCheck` (reject DeclaredValue<=0 or DestinationPort in {"ZZZ"}).
Invoice: `public sealed class InvoiceBuilder { AddLineItem(string,decimal); Invoice Build(string consignmentId); }`.
Pipeline/Chain:
- `public sealed record ShipmentContext(BookingRequest Booking, RateQuote? Quote=null, StowageAssignment? Stowage=null, ClearanceResult? Clearance=null, Invoice? Invoice=null);`
- `public interface IShipmentStage { ShipmentContext Process(ShipmentContext context); }`
- `RatingStage`, `StowageStage(ISlotAllocator)`, `ClearanceStage(IClearanceCheck)`, `InvoiceStage` (Express adds a second "Express service surcharge" line; total == Quote.Amount).
- `public sealed class ShipmentPipeline(IReadOnlyList<IShipmentStage> stages) { ShipmentContext Run(BookingRequest request); }` — folds in order, HALTS before InvoiceStage when `Clearance is { Cleared: false }`.
- `public static class ShipmentPipelineFactory { public static ShipmentPipeline CreateDefault(); }` — composition root, no container.

**Tests** (`ShipmentWorkflow.Tests`, split PipelineTests.cs + RatingTests.cs + InvoiceTests.cs), NSubstitute for `ISlotAllocator`, **12**:
1. End_to_end_booking_produces_an_invoice_with_the_rated_total
2. Express_booking_costs_more_than_the_same_standard_booking
3. Rating_strategy_is_selected_by_service_level
4. Standard_rating_computes_the_expected_amount
5. Express_rating_applies_the_express_multiplier
6. Stowage_stage_assigns_the_first_slot
7. Slot_allocator_can_be_substituted (NSubstitute .Received(1))
8. Clearance_rejection_halts_the_pipeline_before_invoicing (DeclaredValue 0)
9. Embargoed_destination_is_rejected_at_clearance ("ZZZ")
10. Cleared_booking_proceeds_through_every_stage
11. Invoice_builder_sums_line_items_into_the_total
12. Express_invoice_itemizes_freight_and_the_service_surcharge

### ch46 `examples/46-working-with-ai-and-llm-tools/` — ns `RatingReview` (+ `.Analysis`), ch28 toolchain EXACTLY
Three projects + `.slnx` + README, mirroring examples/28-roslyn-analyzers/ (`WireContracts`).

**Consumer `RatingReview.csproj`** (ns `RatingReview`) — pure BCL, hosts the CORRECTED (reviewed) sample so the analyzer is quiet and it builds green:
- `DefaultItemExcludes` for `RatingReview.Analysis/**;RatingReview.Tests/**`.
- `ProjectReference RatingReview.Analysis ... OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.
- `BookingRequest(decimal DeclaredValue, decimal WeightKilograms)`; `IRatingStrategy { decimal Quote(BookingRequest); }`; `StandardRatingStrategy`, `ExpressRatingStrategy` (TWO impls → SHIP0002 quiet).

**Analyzer `RatingReview.Analysis.csproj`** — copy ch28 csproj verbatim: `netstandard2.0`, Nullable enable, TreatWarningsAsErrors, IsPackable=false, IncludeBuildOutput=false, EnforceExtendedAnalyzerRules=true; `Microsoft.CodeAnalysis.CSharp` **5.0.0** PrivateAssets=all (HOST-MATCHED; NOT NuGet-latest → CS9057); `Microsoft.CodeAnalysis.Analyzers` **4.14.0** PrivateAssets=all; AnalyzerReleases.Shipped.md + Unshipped.md as AdditionalFiles. No IsExternalInit shim (plain classes).
- `SingleImplementationStrategyAnalyzer : DiagnosticAnalyzer`, `DiagnosticId="SHIP0002"`, title "Strategy interface has a single implementation", messageFormat "Strategy interface '{0}' has a single implementation '{1}'; confirm the abstraction earns its place", category "ShippingReview", Warning, enabledByDefault. Initialize: EnableConcurrentExecution + ConfigureGeneratedCodeAnalysis(None) + CompilationStart (collect `*Strategy` interfaces + per-interface implementer counts via SymbolAction over NamedType `AllInterfaces`, thread-safe) + CompilationEnd (report each tracked interface with exactly ONE implementer at the interface identifier location).
- Unshipped.md: `SHIP0002 | ShippingReview | Warning | Strategy interface has a single implementation`; Shipped.md: `## Release 1.0` empty skeleton.

**Tests `RatingReview.Tests`** — pin the four standard + `Microsoft.CodeAnalysis.CSharp` 5.0.0; IsTestProject+IsPackable false+NoWarn CA1707; OMIT TestingPlatformDotnetTestSupport; ProjectReference to .Analysis. `AnalyzerTestHarness.cs` copied from ch28 (TPA framework refs, CSharpCompilation.Create, compile-error guard, WithAnalyzers().GetAnalyzerDiagnosticsAsync, filter by DiagnosticId). **7 tests**:
1. Fires_on_a_strategy_interface_with_one_implementation
2. Quiet_on_a_strategy_interface_with_two_implementations
3. Quiet_on_a_single_implementation_interface_not_named_strategy
4. Quiet_when_a_strategy_interface_has_no_implementations
5. Reports_correct_id_severity_and_location (SHIP0002/Warning/interface identifier)
6. Message_names_the_interface_and_its_sole_implementation
7. Flags_each_single_implementation_strategy_interface_independently (2 → Assert.Equal(2,...))

## Diagrams (Wave B, 1 agent owns diagrams.yml, #90 chapter order)
Amber; light-fill emphasis; ~960x560 C4; no baked-in numbers; no cross-language/capstone; short captions.
- #90 `45-final-project` (ch45 PRIMARY): end-to-end slice booking→rating→stowage→clearance→invoice with the clearance halt branch.
- #91 `45-patterns-woven` (ch45 secondary): the patterns at each stage (Factory-selected Strategy, chain of stages, result at clearance, Builder at invoice), each where it earns its place.
- #92 `45-clearance-halts-the-slice` (ch45 density): cleared booking reaching InvoiceStage vs rejected short-circuiting.
- #93 `46-working-with-ai-and-llm-tools` (ch46 PRIMARY): the human-owned review loop — proposed code → analyzer gate (SHIP0002) → collapse or add a real strategy → merge.
- #94 `46-single-implementation-strategy` (ch46 secondary): IRatingStrategy with one impl (flagged) vs two genuine impls (clean).
- #95 `46-analyzer-as-the-gate` (ch46 density): the raw-Roslyn test driving the analyzer, positive fires / negative quiet.

## Waves
- **Wave A (code, 2 agents):** A1 ch45 green (12/12, NSubstitute ISlotAllocator, invoice total == strategy amount, clearance halt). A2 ch46 — build the three ch28-template projects, confirm analyzer FIRES on the single-impl smell + QUIET on the corrected consumer, whole solution GREEN under warnings-as-errors (no CS9057, RS2008 satisfied), then finish 7 tests via raw Roslyn. Both capture path-sanitized `dotnet test`/`build`.
- **Wave B (diagrams):** six figures from GREEN names; append #90-95 chapter order; short captions; no generics/cross-language/capstone; 0 em-dash in SVG text.
- **Wave C (prose, 2):** full spine; REPLACE TODO body; ch45 ADD `## What the slice shows` (ONE AI-sidebar), keep Build-run-observe; ch46 ADD `## Build, run, observe`, NO AI-sidebar; replace TODO caption; embed secondary+density figures; name patterns in place (no number/title lean); "Final Project" never "capstone"; voice self-gate 0 ban-tier; >=2000 words.
- **Wave D (validate, Opus):** AI-sidebar ch45==1/ch46==0; no `## When not to use it` either; Build-run-observe in both; idiomatic 0 (ch45 + ch46 consumer/analyzer); voice 0 ban-tier; em-dash 0 (docs/.cs/README/SVGs/captions); TODO-caption 0; capstone 0; no-cross-language 0; cross-chapter light; caption gate; name-drift (ch45 #90-92, ch46 #93-95, yml 94); dotnet test 12/7 + verify.sh + build.sh 0W/0E (analyzer green under warnings-as-errors); jekyll clean; word counts; toolchain pins (ch45 pure BCL+IVT+4 pins+.slnx; ch46 netstandard2.0 analyzer+CodeAnalysis.CSharp 5.0.0+Analyzers 4.14.0+PrivateAssets+EnforceExtendedAnalyzerRules+AnalyzerReleases+OutputItemType Analyzer+tests pin 4+CodeAnalysis.CSharp 5.0.0+raw-Roslyn harness); path-sanitized. Write verification log.

## Acceptance criteria
See per-chapter detail above. ch45: end-to-end invoice total == rated amount; Standard/Express selected+priced; deterministic slot; substitutable allocator; clearance halt (zero + embargo); itemized express surcharge; Builder total; composition root, no MEDI; one AI-sidebar; no When-not-to-use. ch46: analyzer fires on single-impl `*Strategy`, quiet on two-impl/non-Strategy/zero-impl; SHIP0002/Warning/correct location; message names both; independent per interface; consumer green under warnings-as-errors; ch28 pins exact; Build-run-observe added; ZERO AI-sidebar. Iteration: jekyll clean; yml #90-95 chapter order (94); .slnx each; scripts green; names unchanged; captions short; net10.0/C#14 (analyzer netstandard2.0).

## Risks
- **ch46 Roslyn toolchain (HIGH):** 5.0.0 host-matched pin (CS9057 if bumped); NO .Testing.Xunit (v2 clash) use raw ch28 harness; RS2008 via Unshipped.md; corrected consumer must stay quiet/green. A2 confirms positive/negative + green build before finishing tests.
- **ch46 analyzer logic (MED):** cross-symbol accumulation CompilationStart→SymbolAction→CompilationEnd, concurrent execution on → thread-safe collection; test 7 + two-impl-quiet detect.
- **ch45 over-abstraction irony (MED):** must not become the smell ch46 flags — `IRatingStrategy` ships TWO real impls; every seam justified by a test; `## What the slice shows` states where a direct method sufficed.
- **Capstone/cross-language leak from Notion (HIGH voice):** translate to "Final Project", strip all cross-language; Wave D greps.
- **ch45 decimal (LOW):** in-process decimal allowed (not a wire contract); never described as serialized.
- **Determinism (MED):** SequentialSlotAllocator resets per instance; fresh per CreateDefault(); no clock/Guid in ch45.
- **Standing mechanics:** `{% raw %}` for `{{`/`}}`; .slnx/global.json; CA1707 test-only; path leaks; NSubstitute test-only; IVT via csproj; #90 order; TODO-caption 0; word floor; AI-sidebar ch45==1/ch46==0.

## r19 verification log

Validated 2026-11-01 (Opus, adversarial Wave D). Branch `release/r19`. Every
result below is from reading the actual file or running the actual command.

### Build / test
- **ch45 `dotnet test --solution ShipmentWorkflow.slnx`** — MET. total: 12,
  failed: 0, succeeded: 12. Test bodies read and confirmed: end-to-end
  `Invoice.Total == RatingStrategies.For(...).Quote(...).Amount`; Standard pinned
  at 1,400.00m; Express pinned at 1,960.00m == standard * 1.40m; Express > Standard;
  `RatingStrategies.For` returns the right concrete type per `ServiceLevel`;
  fresh `SequentialSlotAllocator` hands out `SLOT-001`; allocator substituted via
  NSubstitute with `.Received(1)`; clearance halt on DeclaredValue 0 leaves
  `Invoice` null; embargoed "ZZZ" rejected with exact reason, `Invoice` null;
  cleared booking populates every context field; `InvoiceBuilder` sums line items;
  express invoice itemizes Freight + "Express service surcharge", total ==
  `RateQuote.Amount`.
- **ch46 `dotnet test --solution RatingReview.slnx`** — MET. total: 7, failed: 0.
  Test bodies read and confirmed: fires on one-impl `*Strategy` (Assert.Single);
  quiet on two-impl; quiet on non-`Strategy` name (`ISlotAllocator`); quiet on
  zero impls; id==SHIP0002, severity==Warning, span text == interface identifier
  (`IRoutingStrategy`); message contains both interface and sole impl; two
  independent single-impl interfaces each flagged (Assert.Equal(2,...)).
- **`scripts/build.sh`** — MET. Build succeeded, 0 Warning(s), 0 Error(s) across
  all examples. Analyzer builds green under warnings-as-errors; corrected
  two-impl consumer does not trip SHIP0002 (no CS9057, no RS2008).
- **`scripts/verify.sh`** — MET. All suites green; both new examples included
  (45 → 12 passed, 46 → 7 passed).
- **`bundle exec jekyll build`** — MET. done in ~0.5s, exit 0, no Liquid errors.

### ch46 toolchain gates
- Analyzer csproj — MET. netstandard2.0; `Microsoft.CodeAnalysis.CSharp` 5.0.0
  PrivateAssets=all; `Microsoft.CodeAnalysis.Analyzers` 4.14.0 PrivateAssets=all;
  EnforceExtendedAnalyzerRules=true; IsPackable=false; IncludeBuildOutput=false;
  AnalyzerReleases.Shipped.md + Unshipped.md as AdditionalFiles.
- AnalyzerReleases — MET. Shipped.md = `## Release 1.0` empty skeleton table;
  Unshipped.md carries `SHIP0002 | ShippingReview | Warning | ...`.
- Consumer csproj — MET. ProjectReference to analyzer with OutputItemType="Analyzer"
  ReferenceOutputAssembly="false"; pure BCL otherwise (no runtime PackageReference).
- Test csproj — MET. four standard pins (xunit.v3 4.0.2, xunit.runner.visualstudio
  4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0) PLUS
  Microsoft.CodeAnalysis.CSharp 5.0.0; IsTestProject+IsPackable false; NoWarn
  CA1707; no TestingPlatformDotnetTestSupport; no Microsoft.CodeAnalysis.*.Testing
  packages, no xunit v2; raw-Roslyn `AnalyzerTestHarness`.
- DiagnosticDescriptor `customTags: WellKnownDiagnosticTags.CompilationEnd` — MET
  (reports from CompilationEndAction; satisfies RS1037).
- No GlobalSuppressions.cs / example .editorconfig — MET (none found).

### ch45 toolchain gates
- Library csproj — MET. Zero runtime PackageReference (pure BCL);
  `<InternalsVisibleTo Include="ShipmentWorkflow.Tests" />`; no Microsoft.Extensions.*
  (no MEDI); composition root `ShipmentPipelineFactory.CreateDefault` wires with
  plain `new`.
- Test csproj — MET. four standard pins, IsTestProject+IsPackable false, NoWarn
  CA1707, no TestingPlatformDotnetTestSupport.
- .slnx present both examples — MET.
- ch45 not the ch46 smell — MET. `IRatingStrategy` has TWO impls
  (StandardRatingStrategy, ExpressRatingStrategy), would pass SHIP0002.

### Structural rules (non-pattern chapters)
- `## When not to use it` ABSENT both — MET (grep -c == 0 each).
- ch45 AI-sidebar `grep -c '<blockquote class="ai-sidebar"'` == 1 — MET (in
  closing `## What the slice shows`).
- ch46 AI-sidebar == 0 — MET.
- `## Build, run, observe` present both — MET.
- `## What the slice shows` present ch45 — MET.

### Voice + prose
- lgtm-professional-voice: ch45 0 matching lines (0 ban-tier); ch46 0 ban-tier,
  5 watch-tier (defect framing, deliberate-adjective, hedging, intensifiers,
  simply/obviously/easy — 1 each) — MET (gate is 0 ban-tier).
- em-dash across both docs, all new .cs, both READMEs, 6 new SVGs, diagrams.yml —
  MET (0 everywhere).
- capstone across docs, .cs, READMEs, SVGs, captions — MET (0 everywhere).
- No cross-language (java/c++/jvm/spring/rust/golang/virtual-by-default) across
  docs, .cs, READMEs, 6 SVGs, 6 captions — MET. The three bare-`go` hits in ch46
  are English false positives ("stay or go", "go find", "go away").
- Cross-chapter leans — MET (light). Self-referential "this chapter" only; no
  chapter number or title/topic lean. NIT: ch46 line 360 "The analyzer technique
  this book has used elsewhere..." is a light topic pointer, permitted by CLAUDE.md.

### Structure + figures
- Spine order both docs — MET (Learning objectives → Why this matters → primary
  figure after Why → body → Build/run/observe → [ch45 What the slice shows] →
  For deeper coverage).
- `grep -c "TODO caption"` == 0 both — MET. Each doc embeds 3 figures.
- Captions — MET. All carry `Figure NN.N:` prefix, ~20-45 words, no angle-bracket
  generics, 0 em-dash; every type named exists in the example.
- Name-drift — MET. ch45 embeds #90/#91/#92, ch46 embeds #93/#94/#95; every
  `name="..."` resolves to diagrams/<name>.svg AND .excalidraw on disk AND a
  diagrams.yml entry.
- diagrams.yml — MET. 94 entries; six new in chapter order (45s before 46s),
  sections "Ch. 45"/"Ch. 46".
- Idiomatic review (lgtm-idiomatic-review): ch45 0 hits, ch46 0 hits — MET.

### Word counts
- ch45 body prose 2,945 (≥ 2,000) — MET. ch46 body prose 2,877 (≥ 2,000) — MET.

### Verdict
**PASS.** All acceptance criteria MET. Only non-blocking nit: ch46 carries 5
watch-tier voice hits (0 ban-tier) and one light "used elsewhere" topic pointer,
both within policy.
