---
title: "r13 plan — Part 9 FINISH (ch27 source generators, ch28 Roslyn analyzers)"
output: false
render_with_liquid: false
---

# r13 — author ch27, ch28 (Part 9: Idiomatic C#, the two build-time Roslyn chapters)

Opus-planned 2026-10-31. Branch `release/r13` (already created off latest `main`). The two chapters that
complete Part 9. Both are HANDS-ON and both introduce a NEW build-time toolchain that nothing else in this
book uses: a `netstandard2.0` `Microsoft.CodeAnalysis` project consumed via `OutputItemType="Analyzer"
ReferenceOutputAssembly="false"`, tested with the RAW Roslyn driver APIs inside a normal net10.0 xUnit v3
project (NO `.Testing.Xunit` verifier packages, which drag in xUnit v2 and clash with this book's xUnit v3).
This is research-heavy: authority is Microsoft Learn + the Roslyn SDK docs + runnable experiments, NOT blog
posts. Five-wave shape: SPIKE (Roslyn toolchain feasibility gate, runs FIRST) -> A code (2 agents) ->
B diagrams (1 agent, after green) -> C prose (2 agents, each voice-self-gates) -> D validate. Enforces: NO
cross-language comparison (describe source generators, incremental generators, analyzers, diagnostics, AOT
and trimming POSITIVELY and temporally, never against Java/JVM/annotation-processors/C++/Spring/Smalltalk/
"policy-based design"); keep cross-chapter refs light (each chapter stands alone; ch27/ch28 must NOT lean on
ch25/ch26/ch29 even by topic, re-explain in place).

## Scope + example assignments (shipping domain, from the Notion per-chapter map)

- **ch27 Source generators: patterns at build time** (`_docs/27-source-generators.md`, Carrier domain).
  Generate carrier gateway adapter + registry code from a manifest at BUILD time, replacing a
  reflection-based runtime registry, and make the AOT/trimming argument (a compile-time registry needs no
  `Assembly.GetTypes()` scan, so it survives trimming and native AOT). Use an INCREMENTAL source generator
  (`IIncrementalGenerator`) that reads a manifest supplied as an `AdditionalFiles` input and emits
  adapter/registry C#. Cite Microsoft Learn (source generators, incremental generators, `IIncrementalGenerator`,
  `AdditionalText`) + the Roslyn SDK docs (source-generators cookbook) as the PRIMARY sources; GoF not forced
  (Factory/Registry in spirit only, not the subject).
- **ch28 Roslyn analyzers: patterns enforced at compile time** (`_docs/28-roslyn-analyzers.md`,
  Rating/Documentation domain). An analyzer that reports a diagnostic when "money" is modeled as `decimal`
  on a wire-contract type (the fix: integer minor units), a real domain invariant surfaced as a build
  diagnostic. This chapter is the explicit landing point for the book's AI throughline: an analyzer catches
  the pattern-shaped mistakes an LLM tends to emit (an assistant will cheerfully generate a wire DTO with
  `decimal Amount` because it reads naturally), kept in the single AI sidebar. Cite Microsoft Learn
  (Roslyn analyzers, `DiagnosticAnalyzer`, `DiagnosticDescriptor`, writing an analyzer) + the Roslyn SDK
  docs as PRIMARY; GoF not forced.

## Confirmed facts (re-verified this session)

- Part slug `idiomatic-csharp` (both stubs carry it in front matter; verified). Edit body in place.
- Branch `release/r13` is created off latest `main`. SDK pin `10.0.113` (verified `dotnet --version`),
  LangVersion `14.0`, Nullable on, warnings-as-errors, AnalysisLevel latest-recommended, net10.0 TFM, all
  inherited from `examples/Directory.Build.props` for the net10.0 projects. Root `global.json` opts into
  `Microsoft.Testing.Platform`.
- Each stub's primary include is `{% include excalidraw.html name="<NN-...>" caption="Figure NN.1: TODO
  caption." %}`. Primary `name` MUST stay EXACTLY `27-source-generators` and `28-roslyn-analyzers` (verified).
  Replace ONLY the caption text, keeping the `Figure NN.1: ` prefix. `grep -c "TODO caption"` is 1 per stub
  today; MUST be 0 at the end.
- Each stub has a `## When not to use it` section; the single `> **AI sidebar.**` block lives there (exactly
  one per chapter). Each stub also carries a leading `<!-- EXAMPLE ... -->` comment naming the per-chapter
  framing (ch27 "Generating carrier gateway adapters from a manifest at build time"; ch28 "An analyzer
  enforcing that money is never a decimal in a wire contract").
- `_data/diagrams.yml` currently has **57** entries (verified `grep -c "name:"`); append from **#58** in
  chapter order. ONE diagram agent owns diagrams.yml this iteration. Entry shape (verified): `- name:` /
  `section: "Ch. NN"` / `title:` / `caption: >-` folded block. Amber accent; light-fill emphasis boxes with
  dark text; no baked-in chapter numbers; ~960x560 C4-style.
- **THE 3-PROJECT EXAMPLE LAYOUT vs the repo scripts (verified):** `scripts/build.sh` globs
  `examples/*/*.csproj` (TOP-LEVEL only, one directory deep) and `dotnet build -c Release` each.
  `scripts/verify.sh` globs `examples/*/*Tests*.csproj` and `examples/*/*.Tests/*.csproj` and
  `dotnet test --project` each. Consequence, which the layout exploits:
  - The TOP-LEVEL consuming `.csproj` is the only project `build.sh` builds directly. It references the
    generator/analyzer project as an Analyzer, so MSBuild builds that nested project transitively.
  - The nested `*.Tests/` project is the only project `verify.sh` runs (its dir ends in `.Tests`).
  - The nested generator/analyzer project (dir ends in `.Generation` / `.Analysis`, NOT `.Tests`, and is two
    directories deep) is matched by NEITHER build.sh nor verify.sh directly; it is only ever built
    transitively via the consuming project's Analyzer reference and the test project's normal reference. This
    is exactly what we want (a netstandard2.0 project must not be `dotnet test`-ed or built standalone by the
    net10.0 globs).
- **SURVEYED PINS, re-confirmed live and RESOLVED (this is the load-bearing finding of the plan):**
  - NuGet latest stable `Microsoft.CodeAnalysis.CSharp` = **5.9.0**; `Microsoft.CodeAnalysis.Analyzers` =
    **5.9.0** (both verified against api.nuget.org flat index).
  - BUT the SDK 10.0.113 host compiler is **Roslyn 5.0.0** (verified:
    `dotnet /usr/lib64/dotnet/sdk/10.0.113/Roslyn/bincore/csc.dll -version` -> `5.0.0-2.26504.104`;
    `Microsoft.CodeAnalysis.dll` reports a 5.0.x assembly). An analyzer/generator built against a
    `Microsoft.CodeAnalysis` NEWER than the host emits **CS9057** ("the analyzer assembly references version
    ... of the compiler, which is newer than the currently running version ..."), and newer APIs are simply
    absent at load time. Under this repo's warnings-as-errors, CS9057 FAILS the build. Therefore the pin
    steps DOWN to the host's version:
    - **`Microsoft.CodeAnalysis.CSharp` = 5.0.0** (exact match to host csc 5.0.0; `5.0.0` stable exists on
      NuGet, verified). `PrivateAssets="all"`.
    - **`Microsoft.CodeAnalysis.Analyzers` = 4.14.0** (the analyzer-dev best-practices RS rules; these
      analyzers run inside the host compiler too, so they must not be newer than 5.0.0 either; `Analyzers`
      has no 5.0.0 release, it jumps 4.14.0 -> 5.3.0, so 4.14.0 is the safe floor: built against Roslyn 4.x,
      older than the host, loads forward-compatibly, no CS9057). `PrivateAssets="all"`. Set
      `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>` on the generator/analyzer project
      per Roslyn guidance.
    - **Test project** references `Microsoft.CodeAnalysis.CSharp = 5.0.0` directly (net10.0) so the Roslyn
      type identity at test runtime matches the generator/analyzer's compiled references.
    The FEASIBILITY SPIKE below PROVES these pins load and work before any real code is written; if 5.0.0 or
    4.14.0 somehow does not load, the spike records the step-down and the actual working pin. The planner's
    recommendation is 5.0.0 / 4.14.0; do NOT ship 5.9.0 (it will not load under host 5.0.0).
- Namespace survey (re-verified via `grep -rhoP "^namespace" examples/**/*.cs`): taken include
  `Consignments`, `ConsignmentRouting`, `ConsignmentTracking`, `ConsignmentAmendments`, `CarrierGateways`,
  `CarrierIntegration`, `CarrierProvisioning`, `CarrierParcels` (+`.Vendor`), `RateCalculation`,
  `RateComposition`, `RateQuoting`, `RatingEngine`, `RateMath`, `TariffRules`, `TariffExpressions`,
  `TariffTables`, `FreightValueTypes`, `BookingRequests`, `BookingAmendments`, `CustomsClearance`,
  `CustomsDocumentation`, `ManifestWriting`, `TrackingHistory`. The fresh namespaces chosen below do NOT
  collide: **`CarrierManifest`** (ch27 consuming) + **`CarrierManifest.Generation`** (ch27 generator);
  **`WireContracts`** (ch28 consuming) + **`WireContracts.Analysis`** (ch28 analyzer).

## FEASIBILITY SPIKE (Roslyn toolchain) — runs FIRST, gates all of Wave A

Before building either real example, the Wave-A agents prove the pinned Roslyn toolchain actually loads and
runs under SDK 10.0.113. This is the r12 ch25-gate equivalent for the new toolchain. Each Wave-A agent runs
the half relevant to it; together they cover generator-emits AND analyzer-reports. Build a throwaway spike
in a scratch dir (or a scratch target deleted before handback):

1. **Project shape:** a `netstandard2.0` project `Spike.csproj` that inherits `examples/Directory.Build.props`
   but OVERRIDES `<TargetFramework>netstandard2.0</TargetFramework>`, references
   `Microsoft.CodeAnalysis.CSharp 5.0.0` (`PrivateAssets="all"`) and `Microsoft.CodeAnalysis.Analyzers 4.14.0`
   (`PrivateAssets="all"`), sets `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>` and
   `<IsPackable>false</IsPackable>`. Confirm it compiles with 0 warnings under warnings-as-errors (this is
   where RS1xxx / CS9057 would surface).
2. **Trivial `IIncrementalGenerator`** (ch27 agent): a `[Generator(LanguageNames.CSharp)]` that
   `RegisterPostInitializationOutput` emits one marker class. Reference it from a scratch net10.0 consuming
   project via `<ProjectReference ... OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` and
   confirm `dotnet build` on SDK 10.0.113 (a) shows no CS9057, (b) produces the generated marker (verify via
   `EmitCompilerGeneratedFiles=true` or a reference to the generated type that compiles).
3. **Trivial `DiagnosticAnalyzer`** (ch28 agent): a `DiagnosticAnalyzer` with one `DiagnosticDescriptor`
   (Warning) that reports on some trivial symbol. Reference it as an Analyzer from a scratch net10.0 project
   containing a triggering symbol and confirm `dotnet build` surfaces the diagnostic (and, because this repo
   is warnings-as-errors, that a Warning-severity analyzer diagnostic breaks a build).
4. **STEP-DOWN RULE:** if `Microsoft.CodeAnalysis.CSharp 5.0.0` emits CS9057 or fails to load, step DOWN
   further to whatever `5.0.0`-or-lower version the host hosts and record the working version + the reason
   next to the pin. If `Microsoft.CodeAnalysis.Analyzers 4.14.0` surfaces RS issues, record and adjust. Do
   NOT claim any version that does not load. Record the spike outcome (resolved pins, "generator emits" and
   "analyzer reports" both confirmed) in the Wave A handback AND the Wave D log. The spike artifacts are NOT
   committed.

## LOCKED type names (Wave A builds verbatim; Wave B diagrams copy from the green code)

### ch27 `examples/27-source-generators/` (consuming ns `CarrierManifest`, generator ns `CarrierManifest.Generation`)

Layout:
```
examples/27-source-generators/
  CarrierManifest.csproj               (net10.0 lib, TOP level; references the generator as Analyzer;
                                        <AdditionalFiles Include="carriers.manifest" />;
                                        DefaultItemExcludes excludes BOTH CarrierManifest.Generation/** AND
                                        CarrierManifest.Tests/**)
  carriers.manifest                    (AdditionalFiles input; line format "Code,DisplayName,Endpoint")
  ICarrierGateway.cs                   (hand-written contract the generated adapters implement)
  CarrierDispatch.cs                   (hand-written consumer that uses GeneratedCarrierRegistry, proving the
                                        generated code compiles and is callable)
  CarrierManifest.Generation/          (netstandard2.0 generator project, nested)
    CarrierManifest.Generation.csproj
    CarrierGatewayGenerator.cs
    EquatableArray.cs                  (tiny readonly struct wrapper for incremental-caching correctness)
  CarrierManifest.Tests/               (net10.0 xUnit v3, nested; drives the generator via raw Roslyn APIs)
    CarrierManifest.Tests.csproj
    GeneratorEmitTests.cs
    GeneratorCachingTests.cs
  CarrierManifest.slnx
  README.md
```

- Hand-written contract (consuming project): `public interface ICarrierGateway { string Code { get; }
  string DisplayName { get; } string Endpoint { get; } }`.
- Manifest input: `carriers.manifest`, a small text file, 3-4 carrier rows (e.g.
  `DHLX,DHL Express,https://api.dhlx.example`), registered as `<AdditionalFiles Include="carriers.manifest" />`
  in `CarrierManifest.csproj`. The generator trigger is the `AdditionalFiles` item (this is the "manifest
  provided as an AdditionalFiles input" mechanism the brief specifies).
- `[Generator(LanguageNames.CSharp)] public sealed class CarrierGatewayGenerator : IIncrementalGenerator`,
  `Initialize(IncrementalGeneratorInitializationContext context)`:
  - `IncrementalValuesProvider<CarrierEntry>` (or a single `IncrementalValueProvider<CarrierManifestModel>`)
    built from `context.AdditionalTextsProvider.Where(f => f.Path.EndsWith(".manifest",
    StringComparison.OrdinalIgnoreCase)).Select((text, ct) => Parse(text, ct))`, each pipeline stage tagged
    with `.WithTrackingName("...")` so the caching test can assert step reasons.
  - `context.RegisterSourceOutput(model, Emit)` emits, into named hint sources:
    - one adapter per manifest entry: `public sealed class <Code>CarrierGateway : ICarrierGateway` with
      `Code`/`DisplayName`/`Endpoint` returning the manifest values.
    - a registry: `public static class GeneratedCarrierRegistry` with
      `public static IReadOnlyList<ICarrierGateway> All { get; }` (constructs one of each generated adapter)
      and `public static ICarrierGateway? Find(string code)`. This is the "replaces the reflection-based
      registry" headline: a compile-time registry, no `Assembly.GetTypes()`, trim/AOT-safe.
  - Equatable model: `internal sealed record CarrierEntry(string Code, string DisplayName, string Endpoint)`
    and `internal readonly record struct CarrierManifestModel(EquatableArray<CarrierEntry> Entries)`.
    **Caching gotcha (flag in prose + test):** `ImmutableArray<T>` has reference equality by default, which
    breaks incremental caching; `EquatableArray<T>` (a readonly struct wrapping `ImmutableArray<T>` with
    `IEquatable` via `SequenceEqual`) gives the pipeline the value equality it needs to cache correctly.
  - Optional stretch: a `DiagnosticDescriptor` + `context.ReportDiagnostic` for a malformed manifest line
    (shows generators can report diagnostics too). Include only if it does not bloat the chapter.
- **netstandard2.0 generator csproj specifics (see shared rules below):** override TFM to netstandard2.0;
  pin `Microsoft.CodeAnalysis.CSharp 5.0.0` + `Microsoft.CodeAnalysis.Analyzers 4.14.0` both `PrivateAssets="all"`;
  `EnforceExtendedAnalyzerRules=true`; `IsPackable=false`. Keep the generator code to a conservative C#
  subset or add a one-file `IsExternalInit` shim if using `record`/`init` (records on netstandard2.0 need it).
- **Tests** (`CarrierManifest.Tests`, net10.0, raw Roslyn driver, references `Microsoft.CodeAnalysis.CSharp
  5.0.0` + a normal `ProjectReference` to the generator project to instantiate it), ~6:
  1. `Generator_emits_an_adapter_for_each_manifest_entry` — build an in-memory `CSharpCompilation`, provide
     the manifest via a small in-test `AdditionalText` subclass, run
     `CSharpGeneratorDriver.Create(generator.AsSourceGenerator()).AddAdditionalTexts(...).RunGenerators(compilation)`,
     assert `GetRunResult().GeneratedTrees` contains one adapter per entry (by generated source text).
  2. `Generated_registry_wires_all_manifest_entries` — assert the emitted `GeneratedCarrierRegistry.All`
     source references every manifest code.
  3. `Generated_code_compiles_with_no_errors` — `driver.RunGeneratorsAndUpdateCompilation(...)` then assert
     the updated compilation's `GetDiagnostics()` has no errors (covers "generated code compiles").
  4. `Generator_produces_no_adapter_output_without_a_manifest` — run with no `AdditionalText`, assert no
     adapter/registry trees (proves the AdditionalFiles trigger).
  5. `Incremental_pipeline_caches_when_the_manifest_is_unchanged` — create the driver with
     `new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true)`, run once, run again against
     an equivalent compilation, assert the tracked steps report `IncrementalStepRunReason.Cached`/`Unchanged`
     (validates the `EquatableArray`/value-equality model; the incremental/caching lesson).
  6. (optional) `Generator_reports_a_diagnostic_on_a_malformed_manifest_line` — only if the malformed-line
     diagnostic is implemented.

### ch28 `examples/28-roslyn-analyzers/` (consuming ns `WireContracts`, analyzer ns `WireContracts.Analysis`)

Layout:
```
examples/28-roslyn-analyzers/
  WireContracts.csproj                 (net10.0 lib, TOP level; references the analyzer as Analyzer; MUST
                                        BUILD CLEAN; DefaultItemExcludes excludes BOTH WireContracts.Analysis/**
                                        AND WireContracts.Tests/**)
  WireContractAttribute.cs             (marker attribute, the robust detection anchor)
  Contracts.cs                         (COMPLIANT wire-contract types -> no diagnostic -> green build)
  WireContracts.Analysis/              (netstandard2.0 analyzer project, nested)
    WireContracts.Analysis.csproj
    MoneyAsDecimalAnalyzer.cs
  WireContracts.Tests/                 (net10.0 xUnit v3, nested; drives the analyzer via raw Roslyn APIs)
    WireContracts.Tests.csproj
    MoneyAsDecimalAnalyzerTests.cs
    AnalyzerTestHarness.cs
  WireContracts.slnx
  README.md
```

- Marker attribute (consuming project): `[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  public sealed class WireContractAttribute : Attribute` in `namespace WireContracts`. A marker attribute is
  the detection anchor (robust, testable, no fragile name heuristics).
- `[DiagnosticAnalyzer(LanguageNames.CSharp)] public sealed class MoneyAsDecimalAnalyzer : DiagnosticAnalyzer`:
  - `public const string DiagnosticId = "SHIP0001";`
  - static readonly `DiagnosticDescriptor Rule`: id `SHIP0001`; title "Money must not be modeled as decimal
    on a wire contract"; messageFormat "Property '{0}' on wire-contract type '{1}' uses decimal for money;
    use integer minor units"; category "ShippingContracts"; `DiagnosticSeverity.Warning`;
    `isEnabledByDefault: true`; a description; optional `helpLinkUri`. SEVERITY RATIONALE (state in prose):
    analyzers idiomatically ship at Warning and let the consuming team's policy escalate; because this repo
    sets warnings-as-errors, SHIP0001 becomes a BUILD ERROR in the consuming project, which is the
    "design rule as a build error" headline, achieved without hard-coding Error severity.
  - `public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);`
  - `public override void Initialize(AnalysisContext context)`:
    `context.EnableConcurrentExecution();`
    `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);`
    then a `context.RegisterCompilationStartAction(start => { var marker =
    start.Compilation.GetTypeByMetadataName("WireContracts.WireContractAttribute"); if (marker is null)
    return; start.RegisterSymbolAction(ctx => AnalyzeType(ctx, marker), SymbolKind.NamedType); });`
    (the two `Initialize` calls above are exactly what `EnforceExtendedAnalyzerRules` / RS1026 / RS1032 want).
  - `AnalyzeType`: for a named type bearing `WireContractAttribute`, inspect its properties; for any property
    whose type is `SpecialType.System_Decimal`, `ReportDiagnostic` at the property's location with the
    property and type names. ("rates in minor units" is the prose framing of the fix.)
- Compliant consuming types (so the consuming lib builds green):
  `[WireContract] public sealed record RateQuoteContract(string CarrierCode, long AmountMinorUnits, string Currency);`
  `[WireContract] public sealed record BookingChargeContract(long PriceMinorUnits, int Quantity);`
  plus a NON-wire type that uses decimal freely to prove scoping:
  `public sealed record InternalRateCalculation(decimal Amount);` (no attribute -> no diagnostic).
- **netstandard2.0 analyzer csproj specifics:** identical to the generator project (TFM override,
  `Microsoft.CodeAnalysis.CSharp 5.0.0` + `Microsoft.CodeAnalysis.Analyzers 4.14.0` both `PrivateAssets="all"`,
  `EnforceExtendedAnalyzerRules=true`, `IsPackable=false`).
- **Tests** (`WireContracts.Tests`, net10.0, references `Microsoft.CodeAnalysis.CSharp 5.0.0` + a normal
  `ProjectReference` to the analyzer project). `AnalyzerTestHarness` compiles a source string into a
  `CSharpCompilation` (metadata references built from `typeof(object).Assembly.Location` + the netstandard /
  System.Runtime facades, NO extra package), then
  `compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MoneyAsDecimalAnalyzer()))` and
  `await ... .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken)`. Each test source string
  declares its own `namespace WireContracts { [WireContract] ... }` and the attribute so the harness is
  hermetic. ~6 tests:
  1. `Fires_on_decimal_money_in_a_wire_contract` — `[WireContract]` type with `decimal Amount` -> exactly one
     SHIP0001.
  2. `Quiet_on_a_compliant_wire_contract` — `[WireContract]` type with `long AmountMinorUnits` -> zero
     diagnostics.
  3. `Quiet_on_decimal_outside_a_wire_contract` — a non-attributed type with `decimal Amount` -> zero
     diagnostics (proves scoping).
  4. `Reports_correct_id_severity_and_location` — assert `Id == "SHIP0001"`, `Severity ==
     DiagnosticSeverity.Warning`, and the location span lands on the offending property.
  5. `Flags_every_offending_property_on_a_type_with_multiple_violations` — two decimal money properties ->
     two SHIP0001 diagnostics.
  6. `Message_names_the_property_and_type` — assert the formatted message contains the property and type names
     (may fold into #4 if preferred).

## Shared csproj rules for the netstandard2.0 generator/analyzer projects

- `examples/Directory.Build.props` unconditionally sets `<TargetFramework>net10.0</TargetFramework>` and
  LangVersion 14.0 etc. Because Directory.Build.props is auto-imported at the TOP of each csproj, the
  generator/analyzer project OVERRIDES the TFM by declaring `<TargetFramework>netstandard2.0</TargetFramework>`
  in its own `PropertyGroup` (last write wins). Keep `Nullable=enable` and `TreatWarningsAsErrors=true`
  (inherited, wanted). Flag these:
  - **netstandard2.0 + C# 14 language features:** records / `init` / required members / some collection
    expressions need runtime support types absent from netstandard2.0. EITHER keep the generator/analyzer
    code to a conservative subset, OR include a one-file `IsExternalInit` shim (the canonical Roslyn SDK
    workaround) to allow `record`/`init`. The `EquatableArray<T>` helper is written as a plain readonly
    struct so it needs no shim.
  - `RS1041` (compiler extensions should target netstandard2.0) does NOT fire precisely because we target
    netstandard2.0; this is the reason for the TFM. Do not reintroduce it by retargeting.
  - `EnforceExtendedAnalyzerRules=true` + warnings-as-errors means RS1xxx best-practice diagnostics (RS1026
    `EnableConcurrentExecution`, RS1032/RS1038, RS2008 release-tracking for new diagnostic ids, etc.) will
    fail the build if unmet. Honour them: `EnableConcurrentExecution` + `ConfigureGeneratedCodeAnalysis` in
    `Initialize`; satisfy or scope-suppress RS2008 release-tracking for `SHIP0001` with a justification
    (either add the AnalyzerReleases.Shipped/Unshipped files, or a justified NoWarn for RS2008 if the release
    files are noise for a teaching example — follow the analyzer, document the choice).
  - `IsPackable=false` on both the generator/analyzer AND the test projects.
- **Consuming project (net10.0, top-level):** references the generator/analyzer as
  `<ProjectReference Include="..\<Name>.Analysis|Generation\...csproj" OutputItemType="Analyzer"
  ReferenceOutputAssembly="false" />`; `<DefaultItemExcludes>$(DefaultItemExcludes);<GenDir>/**;<TestDir>/**
  </DefaultItemExcludes>` to exclude BOTH nested dirs from its own compile glob; ch28's consuming project
  MUST build CLEAN (all wire types compliant).
- **Test project (net10.0, nested):** pins the FOUR standard packages (xunit.v3 4.0.2,
  xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0), ADDS
  `Microsoft.CodeAnalysis.CSharp 5.0.0`, references the generator/analyzer project as a NORMAL
  `ProjectReference` (so it can `new` the type), sets `IsTestProject=true` + `IsPackable=false` +
  `NoWarn $(NoWarn);CA1707`, and OMITS `TestingPlatformDotnetTestSupport`. A net10.0 project can reference a
  netstandard2.0 project (compatible). NO `.Testing.Xunit` verifier packages (they pull xUnit v2 and clash
  with xUnit v3). Each example has its own `.slnx` and a README mirroring ch22's.

## Waves

- **SPIKE (gate, first thing in Wave A):** as above. Both Wave-A agents confirm the resolved pins
  (5.0.0 / 4.14.0), generator-emits, analyzer-reports, no CS9057, 0 warnings. Record pins + outcome.
- **Wave A (code, 2 agents parallel — one per chapter):** each runs its half of the spike first, then builds
  its example green per the LOCKED names. Run `dotnet test` per example and CAPTURE the real summary,
  path-sanitized (0 `/home/`, 0 username) for the "Build, run, observe" quote. Also confirm
  `dotnet build -c Release` of the consuming project is green (ch27: generated registry compiles and is used
  by `CarrierDispatch`; ch28: consuming lib has zero SHIP0001, builds clean under warnings-as-errors). Wrap
  any fenced `{{`/`}}` in `{% raw %}` if it reaches a `.md` (generated-source string literals, interpolation,
  collection expressions). Each agent reports: the resolved pins, the spike outcome (emit/report confirmed),
  the real `dotnet test` summary, and the final LOCKED names actually built (so Wave B copies truth).
- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml from #58):** copy LOCKED real type names
  from the compiled examples. Paired SVG + `.excalidraw` per figure, amber accent, light-fill emphasis boxes
  with dark text, no baked-in chapter numbers, ~960x560 C4-style. Append to `_data/diagrams.yml` in chapter
  order from #58. Two figures/chapter required; add the third (density) figure for any chapter whose body
  passes ~2500 words (confirm final count with Wave C):
  - **#58 `27-source-generators`** (primary; name MUST match the ch27 stub include): `carriers.manifest`
    (AdditionalFiles) -> `CarrierGatewayGenerator` (`IIncrementalGenerator`) at build time -> emitted
    `<Code>CarrierGateway` adapters + `GeneratedCarrierRegistry`, replacing a runtime reflection registry
    (trim/AOT-safe).
  - **#59 `27-incremental-generator-pipeline`** (secondary): the incremental pipeline —
    `AdditionalTextsProvider` -> parse to the equatable `CarrierManifestModel` -> `RegisterSourceOutput`;
    unchanged input is cached.
  - **(#60 `27-generated-registry-vs-reflection`, density if ch27 > ~2500):** compile-time registry vs an
    `Assembly.GetTypes()` scan; trimming/native-AOT.
  - **#61 `28-roslyn-analyzers`** (primary; name MUST match the ch28 stub include): `MoneyAsDecimalAnalyzer`
    inspecting `[WireContract]` types during compilation and reporting `SHIP0001` on `decimal` money
    properties, surfaced as a build diagnostic; a `long` minor-units contract passes.
  - **#62 `28-analyzer-diagnostic-pipeline`** (secondary): `CompilationStart` -> resolve
    `WireContractAttribute` -> symbol action over named types -> report at the property location.
  - **(#63 `28-wire-contract-rule`, density if ch28 > ~2500):** the domain rule (money never decimal on the
    wire; minor units) and the AI-throughline framing (the analyzer catches the LLM-shaped mistake).
  Real captions describing only types that exist in the green code; no cross-language tokens anywhere in
  captions or SVG labels.
- **Wave C (prose, 2 agents, after code + figures — one per chapter):** write the full spine and replace the
  TODO caption text (keep the `Figure NN.1: ` prefix). Spine: Learning objectives -> Why this matters ->
  primary FIGURE (after "Why this matters") -> body (secondary/density figures where density demands) ->
  `## Build, run, observe` (quotes the REAL path-sanitized `dotnet test` summary, 0 `/home/`, 0 username;
  ch27 may also note the consuming build emitting the registry; ch28 points at the fire-on-bad test since the
  consuming lib stays green) -> `## When not to use it` (exactly ONE `> **AI sidebar.**`) ->
  `## For deeper coverage` (Microsoft Learn + the Roslyn SDK docs as PRIMARY).
  - ch27: describe source generators and INCREMENTAL generators POSITIVELY — the `AdditionalFiles` manifest
    input, the equatable-model caching contract, emitting adapters + a compile-time registry, and the
    AOT/trimming argument (no reflection scan). Re-explain any registry/factory idea in place; do not lean on
    the other Part 9 chapters.
  - ch28: describe analyzers POSITIVELY — `DiagnosticAnalyzer`, `DiagnosticDescriptor`, symbol actions,
    resolving a marker attribute at `CompilationStart`, reporting at a location, and severity-as-policy (a
    Warning that warnings-as-errors turns into a build failure). The AI sidebar is the explicit landing point
    for the book's AI throughline: an analyzer encodes a domain invariant that an LLM assistant will
    routinely violate (`decimal Amount` on a wire DTO), so the build catches the pattern-shaped mistake.
  - BOTH: zero em-dash; no rule-of-three; 0 ban-tier; idiomatic C# 14 only; NO cross-language (no
    annotation-processor/Java/C++ analogy for generators/analyzers); cross-chapter refs light; wrap `{{`/`}}`
    fenced blocks in `{% raw %}`. Body >= 2000 words HARD floor, 2500 target, no ceiling; add the reserved
    density figure (#60/#63) if a body runs past ~2500.
  - **Voice self-gate (mandatory, r11/r12 lesson):** each prose agent MUST run
    `lgtm-professional-voice/scripts/scan.sh` on its OWN chapter file and reach 0 ban-tier BEFORE handing
    back. Avoid-list to check and remove: "genuinely", "deliberately", "earns its keep"/"earns its place"/
    "earns the"/"is worth its", "for free", "plainly", the filler "actually", and editorializing kickers.
- **Wave D (validate):**
  - `lgtm-idiomatic-review` — 0 hits on both examples (all three projects each).
  - `lgtm-professional-voice` scanner — 0 ban-tier on both docs (grep the editorializing families; confirm
    the avoid-list words are absent).
  - Em-dash == 0 across the 2 docs + all `.cs` + all README.
  - `grep -c "TODO caption"` across the 2 docs == 0.
  - Diagram name-drift gate: every SVG label + every caption token present in the matching example source.
  - NO-CROSS-LANGUAGE gate: grep the 2 docs, both example trees (.cs + README), the new diagrams.yml captions
    (#58 onward) and the new SVGs for
    `Java|JVM|erasure|annotation.process|C\+\+|CRTP|Spring|Smalltalk|policy-based` (case-insensitive) == 0.
  - CROSS-CHAPTER gate: no numeric cross-refs, no "the X chapter"/title/topic pointers; ch27/ch28 do not lean
    on ch25/ch26/ch29.
  - `dotnet test` per example (ch27 ~6, ch28 ~6) + `scripts/verify.sh` (both auto-discovered via
    `*.Tests/*.csproj`, all green alongside the rest).
  - `scripts/build.sh` builds both consuming libs green (which transitively builds the netstandard2.0
    generator/analyzer projects); confirm ch28's consuming lib is 0-diagnostic under warnings-as-errors.
  - **TOOLCHAIN gates (new this iteration):** confirm the generator/analyzer projects are TargetFramework
    netstandard2.0 (not net10.0); confirm the resolved Roslyn pins (`Microsoft.CodeAnalysis.CSharp 5.0.0`,
    `Microsoft.CodeAnalysis.Analyzers 4.14.0`, both PrivateAssets=all) with NO CS9057 and
    `EnforceExtendedAnalyzerRules=true`; confirm the NO-xUnit-v2 testing approach held (NO
    `.Testing.Xunit` / `.Analyzer.Testing.Xunit` package anywhere; tests drive raw `CSharpGeneratorDriver` /
    `compilation.WithAnalyzers`); confirm build.sh/verify.sh glob compatibility for the 3-project layout (the
    generator/analyzer project is built only transitively and never `dotnet test`-ed).
  - `bundle exec jekyll build` clean (all new figures render).
  - Word counts recorded for both bodies.
  - Confirm diagrams.yml appended from #58 in chapter order; both test csprojs omit
    `TestingPlatformDotnetTestSupport`, pin the four packages + `Microsoft.CodeAnalysis.CSharp 5.0.0`, set
    IsTestProject + IsPackable false + NoWarn CA1707; each example its own `.slnx`.
  - Write the verification log.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if > ~2500); full spine with the primary figure
AFTER "Why this matters"; figure pair(s) on disk + registered in diagrams.yml with matching `name` and REAL
captions (no TODO); example `dotnet test` green and included by `scripts/verify.sh`; consuming lib built
green by `scripts/build.sh`; tests assert behaviour; "Build, run, observe" quotes real path-sanitized output
(0 `/home/`, 0 username); exactly one AI sidebar in "When not to use it"; zero em-dash across docs + .cs +
README; no rule-of-three; 0 ban-tier; idiomatic C# 14 only; NO cross-language; cross-chapter refs light; no
numeric cross-refs; every figure/caption type exists in the example; primary citations Microsoft Learn +
Roslyn SDK docs.
- ch27 proves: the incremental generator reads the `carriers.manifest` AdditionalFiles input and emits one
  adapter per entry; the generated `GeneratedCarrierRegistry` wires every entry; the generated code compiles
  with no errors; no adapter output without a manifest; the pipeline caches on unchanged input (the
  equatable-model lesson). Spike outcome + resolved pins recorded.
- ch28 proves: SHIP0001 fires on `decimal` money in a `[WireContract]` type; is quiet on a `long`
  minor-units contract; is quiet on `decimal` outside a wire contract (scoping); reports the correct
  id/severity/location; flags every offending property on a multi-violation type. The consuming lib builds
  clean (violations live only in the tests). AI throughline landed in the single sidebar.

Iteration: spike-resolved Roslyn pins recorded (5.0.0 / 4.14.0, no CS9057); jekyll clean; diagrams.yml
appended from #58 in chapter order; both test csprojs omit `TestingPlatformDotnetTestSupport` and pin the
four packages + `Microsoft.CodeAnalysis.CSharp 5.0.0`; generator/analyzer projects are netstandard2.0 with
PrivateAssets=all + EnforceExtendedAnalyzerRules=true + IsPackable false; NO `.Testing.Xunit` verifier
package anywhere; each example own `.slnx`; no cross-language token anywhere; ch27 primary figure name stays
`27-source-generators`, ch28 `28-roslyn-analyzers`; Part 9 COMPLETE (ch25-29).

## Risks

- **Roslyn version load / feasibility (HIGH):** the SDK 10.0.113 host compiler is Roslyn 5.0.0; the NuGet
  "latest stable" 5.9.0 is NEWER than the host and will emit CS9057 (fatal under warnings-as-errors) and lack
  APIs at load. Mitigation: the SPIKE runs first and pins DOWN to `Microsoft.CodeAnalysis.CSharp 5.0.0`
  (host-matched) + `Microsoft.CodeAnalysis.Analyzers 4.14.0` (older-than-host, no CS9057); record the working
  pins and the "generator emits / analyzer reports" confirmations. Never ship a version that does not load.
- **xUnit-v2 verifier clash (avoided by construction):** do NOT add `Microsoft.CodeAnalysis.CSharp.
  SourceGenerators.Testing.Xunit` / `...Analyzer.Testing.Xunit` (they depend on xUnit v2 and clash with this
  book's xUnit v3 4.0.2). Tests use the raw Roslyn driver (`CSharpGeneratorDriver.GetRunResult()` for the
  generator; `compilation.WithAnalyzers(...).GetAnalyzerDiagnosticsAsync()` for the analyzer) in a normal
  net10.0 xUnit v3 project. Wave D greps for any `.Testing.Xunit` package == 0.
- **netstandard2.0 project inheriting net10.0-only Directory.Build.props (HIGH):** the shared props force
  net10.0; the generator/analyzer project MUST override `<TargetFramework>netstandard2.0</TargetFramework>`.
  C# 14 features (records/`init`/required/collection expressions) need runtime shims on netstandard2.0; keep
  the code conservative or add a one-file `IsExternalInit` shim. Keep Nullable + warnings-as-errors.
- **AdditionalFiles / analyzer-config plumbing:** the manifest only reaches the generator if registered as
  `<AdditionalFiles Include="carriers.manifest" />` in the consuming csproj and matched in
  `AdditionalTextsProvider`; the analyzer only sees the marker type if resolved via
  `GetTypeByMetadataName("WireContracts.WireContractAttribute")` at `CompilationStart`. Both are covered by a
  dedicated test (generator "no output without manifest"; analyzer "quiet outside a wire contract").
- **EnforceExtendedAnalyzerRules RS* diagnostics:** `Initialize` must call `EnableConcurrentExecution` +
  `ConfigureGeneratedCodeAnalysis` (RS1026/RS1032); new diagnostic id `SHIP0001` triggers RS2008
  release-tracking (add AnalyzerReleases files or a justified RS2008 NoWarn). Follow the analyzer, do not
  fight warnings-as-errors.
- **Incremental-generator caching correctness (ch27):** `ImmutableArray<T>` has reference equality and breaks
  caching; use an `EquatableArray<T>` value-equality wrapper and tag pipeline stages with `.WithTrackingName`
  so the caching test can assert `Cached`/`Unchanged`. The caching test is the guard.
- **Consuming lib must stay violation-free to build green while tests exercise violations (ch28):** the
  consuming project models only compliant (`long` minor-units) wire types + one non-wire `decimal` type; all
  violation cases live in the test project fed to the analyzer via Roslyn. If any violating sample leaks into
  the consuming lib, warnings-as-errors breaks `scripts/build.sh`. Wave D builds the consuming lib to confirm.
- **build.sh / verify.sh glob compatibility for the 3-project layout:** build.sh (`examples/*/*.csproj`)
  builds only the top-level consuming lib (generator built transitively); verify.sh
  (`examples/*/*.Tests/*.csproj`) runs only the `.Tests` project; the `.Generation`/`.Analysis` project
  (two dirs deep, not `.Tests`) is never globbed. The consuming csproj must exclude BOTH nested dirs via
  `DefaultItemExcludes` or it will try to compile their .cs. Verified against the current scripts.
- **Diagram name drift:** figures generated AFTER green code with locked names; Wave D verifies every SVG
  label + caption token exists in the example source.
- **TODO-caption trap:** the primary-figure include caption is owned by neither the diagram nor the code
  agent by default; Wave C explicitly replaces it (keeping `Figure NN.1: `); Wave D greps `TODO caption` == 0.
- **Voice regression:** executor greps are not enough; each Wave-C prose agent runs the voice scanner to 0
  ban-tier before handback (avoid-list + editorializing kickers).
- **Word floor:** 2000-word hard floor per chapter; add the reserved density figure (#60/#63) if a body
  passes ~2500 rather than trimming substance.
- **Standing mechanics:** Liquid `{{ }}` in fenced blocks (generated-source string literals, interpolation,
  collection expressions) wrapped in `{% raw %}`; `.slnx`/`global.json` wiring; CA1707 test-only; path leaks
  in quoted output; no cross-language analogy for generators/analyzers (a tempting slip at "annotation
  processors" / "compile-time metaprogramming", stay positive and temporal).

## r13 verification log

Opus-validated 2026-10-31 (branch `release/r13`). Verdict: PASS (PASS-WITH-NITS at validation; both
nits fixed in a repair pass: ch27 "earns its name" reworded; ch28
`Reports_correct_id_severity_and_location` tightened to assert the diagnostic span lands exactly on the
`Amount` property identifier). Part 9 (ch25-29) is COMPLETE with ch27 (source generators) + ch28
(Roslyn analyzers).

- Tests (live): `examples/27-source-generators` 5 passed / 0 failed; `examples/28-roslyn-analyzers`
  6 passed / 0 failed; zero warnings under warnings-as-errors. `scripts/verify.sh` green (both
  auto-discovered via `*.Tests/*.csproj`, whole suite green). `scripts/build.sh` `0 Warning(s)
  0 Error(s)` (consuming libs build; netstandard2.0 generator/analyzer built only transitively; ch28
  consuming lib is 0-SHIP0001).
- Resolved Roslyn pins (NEW toolchain this iteration): `Microsoft.CodeAnalysis.CSharp` = 5.0.0
  (host-matched to SDK 10.0.113's Roslyn 5.0.0) and `Microsoft.CodeAnalysis.Analyzers` = 4.14.0
  (older-than-host, no CS9057; Analyzers has no 5.0.0, so 4.14.0 is the safe floor), both
  `PrivateAssets="all"`, with `EnforceExtendedAnalyzerRules=true`. No CS9057 in any build.
  Generator/analyzer projects are `netstandard2.0` (override of the net10.0 Directory.Build.props),
  `IsPackable=false`, `IncludeBuildOutput=false`; ch27 ships an `IsExternalInit` shim for records on
  netstandard2.0.
- Testing approach: xUnit v3 only (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1,
  Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0) + direct `Microsoft.CodeAnalysis.CSharp` 5.0.0. NO
  `.Testing.Xunit` / `.Analyzer.Testing.Xunit` verifier package anywhere (the only textual hits are
  explanatory comments). Tests drive the raw `CSharpGeneratorDriver`
  (`GetRunResult`/`RunGeneratorsAndUpdateCompilation`, `trackIncrementalGeneratorSteps:true`) and
  `compilation.WithAnalyzers(...).GetAnalyzerDiagnosticsAsync()`. Test projects omit
  `TestingPlatformDotnetTestSupport`, set `IsTestProject`+`IsPackable=false` and `NoWarn;CA1707`. Each
  example has its own `.slnx`.
- ch28 RS2008 satisfied via `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` (SHIP0001
  declared Unshipped), not a NoWarn.
- Content: full chapter spine both (primary figure after "Why this matters"); `## Build, run, observe`
  quotes real path-sanitized `dotnet test` output (0 `/home/`, 0 username, counts 5/6); exactly one
  `> **AI sidebar.**` per chapter in "When not to use it" (ch28 lands the AI throughline: the analyzer
  catches the `decimal Amount` wire-DTO mistake an LLM routinely emits); "For deeper coverage" cites
  Microsoft Learn + the Roslyn SDK as primary. Body prose ~2917 (ch27) / ~2634 (ch28) words.
- Figures: ch27 references 3 (`27-source-generators`, `27-incremental-generator-pipeline`,
  `27-generated-registry-vs-reflection`), ch28 references 2 (`28-roslyn-analyzers`,
  `28-analyzer-diagnostic-pipeline`); all paired `.svg`+`.excalidraw` on disk and registered in
  `_data/diagrams.yml` (now 62 entries, the 5 new ones appended in chapter order). `bundle exec jekyll
  build` clean; all figures render and copy to `_site/diagrams/`.
- Gates: no cross-language token in docs/.cs/README/new diagrams.yml captions/new SVGs (incl.
  annotation-processor); cross-chapter refs light (0 numeric/topic pointers; no lean on ch25/26/29);
  diagram name-drift 0 (density-figure BCL/AOT terms excepted); `grep -c "TODO caption"` = 0; "capstone"
  = 0; em-dash = 0 across docs + .cs + READMEs (one slipped into a ch27 test comment, removed);
  `lgtm-professional-voice` 0 ban-tier both; `lgtm-idiomatic-review` 0 hits both example trees.

**Part 9 is COMPLETE (ch25-29).**
