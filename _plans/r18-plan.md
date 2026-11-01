---
title: "r18 plan — Part 13 (ch43, ch44)"
output: false
render_with_liquid: false
---

# r18 — author ch43, ch44 (Part 13: Changing Inherited Code, second half)

Opus-planned 2026-11-01. Branch `release/r18` (based on main@aba3059, r17 merge).
Second half of the legacy / Feathers track: the refactoring catalogue and the
dependency-breaking catalogue contrasted and combined on one engine (ch43), and
the refactor-or-rewrite judgement behind a stable seam (ch44). Reuses the r17
Part-13 design verbatim. Standard net10.0 library + xUnit v3 toolchain.

## REUSED locked Part-13 design (from r17 — non-negotiable)

- Before/after allowed ONLY in Part 13. Each example's "before" ships in a nested
  `<Root>.Legacy` namespace with `Legacy*` names, each legacy type carrying an
  XML-doc `<summary>` stating it is an intentionally untestable legacy shape shown
  for contrast. Legacy form COMPILES GREEN under net10.0 + warnings-as-errors +
  analyzers. CA1822 (or any analyzer firing on an ambient static) fixed with a
  MEMBER-SCOPED `[SuppressMessage]` carrying a Part-13 justification, never
  assembly-wide / GlobalSuppressions.cs / .editorconfig.
- "Untestability" / "no stable seam" proven by a deterministic REFLECTION test
  (sealed / parameterless-ctor-only / non-virtual), never a flaky wall-clock/Guid
  assertion.
- Refactored/shipped forms are clean idiomatic C# 14 in the root namespace and are
  what the behaviour tests exercise. idiomatic-review 0 on refactored forms;
  legacy-form findings intentional/documented.
- Pure BCL libraries (no runtime PackageReference). NSubstitute test-only (four
  standard pins: xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1,
  Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0). NO MEDI. `InternalsVisibleTo`
  via csproj. Each example its own `.slnx`; test csproj OMITS
  TestingPlatformDotnetTestSupport, sets IsTestProject + IsPackable false + NoWarn
  CA1707. Root global.json opts into Microsoft.Testing.Platform.
- NO-CROSS-LANGUAGE everywhere (prose, comments, README, SVG labels, captions):
  never Java/C++/JVM/Spring/Rust/Go, never "virtual-by-default language"/"unlike X".
  Positive C# framing; temporal thesis. Feathers (*Working Effectively with Legacy
  Code*) and Fowler (*Refactoring* 2nd ed.) cited as the SOURCE WORKS of the two
  catalogues, not a language contrast.
- Spine: Learning objectives -> Why this matters -> primary FIGURE (after Why) ->
  body -> Build, run, observe (real path-sanitized `dotnet test`; 0 `/home/`, 0
  username) -> `## When not to use it` (ADD it; exactly ONE
  `<blockquote class="ai-sidebar" markdown="1">`, no inline lead-in, NOT the IAL
  form) -> For deeper coverage (Feathers + Fowler via `/bibliography/` + publisher
  links; Microsoft Learn for the C# mechanics). Replace `Figure NN.1: TODO caption.`
  keeping the `Figure NN.1: ` prefix. Captions SHORT (1-2 sentences, ~20-45 words,
  no angle-bracket generics, zero em-dash). Body >= 2000 floor, 2500 target, add +
  embed a density figure past ~2500.

## Confirmed facts

- Part slug `changing-inherited-code` (both stubs). Primary includes LOCKED:
  `43-fowler-vs-feathers`, `44-refactor-or-rewrite`. Neither stub has
  `## When not to use it` yet (Wave C adds it). `grep -c "TODO caption"` == 1 each
  today, 0 at end.
- diagrams.yml = 82 entries; highest label #83 (#80 reserved-but-unused in r17).
  r18 appends from **#84** in chapter order (all ch43 then all ch44).
- Bibliography: Feathers https://www.oreilly.com/library/view/working-effectively-with/0131177052/ ;
  Fowler https://martinfowler.com/books/refactoring.html .
- Template: examples/41-seams-non-virtual-by-default/. Equivalence idiom:
  examples/40-anti-patterns-and-pattern-misuse/FreightRating.Tests/EquivalenceTests.cs
  (`TheoryData<RateRequest>` + `[Theory][MemberData]` + `Assert.Equal`). Reflection
  no-seam idiom: examples/41-.../RateEngineSeams.Tests/LegacyContrastTests.cs.
- Fresh namespaces: `RateEngineCatalogues` (+ `.Legacy`) ch43; `RateEngineReplacement`
  (+ `.Legacy`) ch44.

## LOCKED type names (Wave A builds verbatim)

### ch43 `examples/43-fowler-vs-feathers/` — ns `RateEngineCatalogues` (+ `.Legacy`), pure BCL
Project `RateEngineCatalogues.csproj`; tests `RateEngineCatalogues.Tests`;
`RateEngineCatalogues.slnx`; README. csproj: no runtime PackageReference;
`<InternalsVisibleTo Include="RateEngineCatalogues.Tests" />`.

Shared domain (root):
- `public enum ServiceLevel { Standard, Express, Overnight }`
- `public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, ServiceLevel Service);`
- `public sealed record RateQuote(string ConsignmentId, decimal Amount, DateTimeOffset QuotedAt);`

Seams shared by both non-legacy forms (root):
- `public interface ITariffSource { decimal RatePerKilogram(string laneCode); }` + `public sealed class StandardTariffSource : ITariffSource` (2.40m for "STD").
- `public interface IClock { DateTimeOffset UtcNow { get; } }` + `public sealed class SystemClock : IClock`.
- `internal static class PeakWindow { internal static bool IsPeak(DateTimeOffset at) => at.Hour is >= 8 and < 18; }`

Legacy, untestable (nested `.Legacy`; XML-doc labelled):
- `public sealed class LegacyTariffTable` — instance dict; `public decimal RatePerKilogram(string laneCode)`.
- `public sealed class LegacyRateEngine` — sealed/non-virtual/parameterless. `public decimal Quote(decimal declaredValue, decimal weightKilograms, string serviceLevel)`: hard-coded `new LegacyTariffTable()`; `declaredValue * 0.015m`; weight; string `switch` on serviceLevel (EXPRESS *=1.25m, OVERNIGHT *=1.5m); `DateTime.Now` peak *=1.1m. Member-scoped CA1822 suppress if fires.

Feathers stage — minimal seams, under test (root):
- `public sealed class SeamedRateEngine(ITariffSource tariffs, IClock clock)` — loose signature + inline switch + inline literals KEPT (structure unchanged). `public decimal Quote(decimal declaredValue, decimal weightKilograms, string serviceLevel)`.

Fowler stage — refactored behind green tests (root):
- `public interface IServiceSurcharge { decimal Apply(decimal subtotal); }` + `StandardSurcharge`(=>subtotal), `ExpressSurcharge`(*1.25m), `OvernightSurcharge`(*1.5m); `public static class ServiceSurcharges { public static IServiceSurcharge For(ServiceLevel level); }`.
- `internal static class RateRules { internal const decimal DeclaredValueRate = 0.015m; internal const decimal PeakMultiplier = 0.1m; internal const string StandardLane = "STD"; }`
- `public sealed class RateEngine(ITariffSource tariffs, IClock clock)`: `public RateQuote Quote(RateRequest request)` (Introduce Parameter Object) with private `BaseCharge`/`WeightCharge`/`PeakSurcharge` (Extract Method), `ServiceSurcharges.For(request.Service).Apply(...)`.

Test doubles: `FakeClock(DateTimeOffset utcNow) : IClock`; NSubstitute `ITariffSource`.

**Tests** (split FowlerFeathersTests.cs + EquivalenceTests.cs + LegacyContrastTests.cs), 11:
1. Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock
2. Refactored_engine_applies_the_peak_surcharge_during_the_peak_window
3. Refactored_engine_applies_the_express_service_surcharge
4. Refactored_engine_applies_the_overnight_service_surcharge
5. Refactored_engine_stamps_the_quote_with_the_injected_clock
6. Injected_tariff_source_can_be_substituted (NSubstitute .Received(1))
7. Service_surcharge_is_selected_polymorphically_by_level ([Theory])
8. Internal_service_rules_are_reachable_through_internals_visible_to ([Theory])
9. Both_catalogues_produce_the_same_quote ([Theory][MemberData] equivalence: SeamedRateEngine.Quote(...) == RateEngine.Quote(req).Amount under same tariff+fixed clock)
10. Legacy_engine_offers_no_seam_before_feathers_opens_one (reflection)
11. Standard_tariff_source_returns_the_expected_rate

### ch44 `examples/44-refactor-or-rewrite/` — ns `RateEngineReplacement` (+ `.Legacy`), pure BCL
Project `RateEngineReplacement.csproj`; tests `RateEngineReplacement.Tests`;
`RateEngineReplacement.slnx`; README. csproj: no runtime PackageReference;
`<InternalsVisibleTo Include="RateEngineReplacement.Tests" />`.

Shared domain (root):
- `public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, string LaneCode);`
- `public sealed record RateQuote(string ConsignmentId, decimal Amount, DateTimeOffset QuotedAt);`

Stable seam/contract (root): `public interface IRateEngine { RateQuote Quote(RateRequest request); }`

Collaborators (root): `ITariffSource`/`StandardTariffSource`; `IClock`/`SystemClock`; `internal static class PeakWindow { internal static bool IsPeak(DateTimeOffset at) => at.Hour is >= 8 and < 18; }`.

Legacy, no stable seam (nested `.Legacy`; XML-doc labelled):
- `public sealed class LegacyTariffTable` — instance dict; `RatePerKilogram`.
- `public sealed class LegacyRateEngine` — sealed/non-virtual/parameterless, **does NOT implement `IRateEngine`**. `public decimal Quote(RateRequest request)`: `new LegacyTariffTable()`, inline `0.015m`, `DateTime.Now` peak. Member-scoped CA1822 suppress if fires.

Two destinations behind the SAME interface (root):
- `internal static class RateRules { internal const decimal DeclaredValueRate = 0.015m; internal const decimal PeakMultiplier = 0.1m; }`
- `public sealed class RefactoredRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine` (refactor-in-place, stepwise).
- `public sealed class RewrittenRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine` (clean rewrite, same contract).
- `public sealed class QuoteService(IRateEngine engine) { public RateQuote QuoteFor(RateRequest request); }` (bound only to the seam).

Test doubles: `FakeClock(DateTimeOffset utcNow) : IClock`; NSubstitute `ITariffSource`.

**Tests** (split RefactorOrRewriteTests.cs + EquivalenceTests.cs + LegacyContrastTests.cs), 11:
1. Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock
2. Refactored_engine_applies_the_peak_surcharge_during_the_peak_window
3. Rewritten_engine_quotes_a_deterministic_amount_with_a_fixed_clock
4. Rewritten_engine_applies_the_peak_surcharge_during_the_peak_window
5. Both_implementations_satisfy_the_same_contract ([Theory][MemberData] equivalence; record value equality, same fixed clock)
6. Quote_service_works_with_the_refactored_engine_behind_the_seam
7. Quote_service_works_with_the_rewritten_engine_behind_the_seam (one-line swap)
8. Injected_tariff_source_can_be_substituted_in_both_engines ([Theory], NSubstitute .Received(1))
9. Internal_peak_window_is_reachable_through_internals_visible_to ([Theory])
10. Legacy_engine_has_no_stable_seam_to_refactor_or_rewrite_behind (reflection: sealed, parameterless ctor, non-virtual, !IRateEngine.IsAssignableFrom)
11. Standard_tariff_source_returns_the_expected_rate

## Diagrams (Wave B, 1 agent owns diagrams.yml, append from #84 in chapter order)
Amber; light-fill emphasis boxes; no baked-in chapter numbers; ~960x560 C4; SHORT
captions; paired svg+excalidraw. Ship all six, embed density in each body.
- **#84 `43-fowler-vs-feathers`** (ch43 PRIMARY): legacy -> Feathers seams/under-test -> Fowler refactors behind green tests -> combined, not rivals.
- **#85 `43-feathers-then-fowler`** (ch43 secondary): the two catalogues side by side joined by "tests stay green."
- **#86 `43-behaviour-preserved`** (ch43 density): the equivalence [Theory] (seamed == refactored under one fixed clock).
- **#87 `44-refactor-or-rewrite`** (ch44 PRIMARY): legacy no-seam -> stable `IRateEngine` with two implementations + `QuoteService` consumer.
- **#88 `44-decision-criteria`** (ch44 secondary): criteria matrix (test coverage, change frequency, blast radius, domain stability).
- **#89 `44-same-contract-two-paths`** (ch44 density): equivalence [Theory]; one-line consumer swap.

## Waves
- **Wave A (code, 2 parallel):** build each example green per LOCKED names; both forms compile; legacy green under warnings-as-errors (member-scoped CA1822 w/ Part-13 justification if fires, recorded); tests exactly as listed (11/11); reflection no-seam proof; equivalence [Theory] via ch40 idiom; NSubstitute drives `ITariffSource`. Capture path-sanitized summary.
- **Wave B (diagrams):** six figures from GREEN locked names; append diagrams.yml from #84 chapter order; short captions, no generics/cross-language.
- **Wave C (prose, 2):** full spine; REPLACE TODO body; ADD When-not-to-use (one AI-sidebar component); replace TODO caption; embed secondary + density figures; ch43 = Feathers-then-Fowler combined; ch44 = criteria + stable-seam equivalence; voice self-gate 0 ban-tier; >= 2000 words.
- **Wave D (validate, Opus):** idiomatic 0 on refactored; voice 0 ban-tier; em-dash 0; TODO-caption 0; capstone 0; AI-sidebar==1/ch; caption gate; name-drift gate; no-cross-language grep 0; cross-chapter gate; legacy-label gate; dotnet test 11/11 each + verify.sh + build.sh 0W/0E; jekyll clean; word counts; diagrams.yml #84 order; toolchain pins; .slnx; pure BCL; InternalsVisibleTo. Write verification log below.

## Acceptance criteria
Per chapter: body >= 2000; full spine w/ primary figure after Why; When-not-to-use
added before For-deeper-coverage; figures on disk + registered w/ short captions;
dotnet test green + in verify.sh; both legacy + refactored compile green; Build-run-observe
path-sanitized (0 /home/, 0 username); one AI-sidebar component; 0 em-dash; no rule-of-three;
0 ban-tier; no capstone; no cross-language; cross-chapter light; legacy labelled; Feathers+Fowler
cited via bibliography + publisher links + MS Learn.
- ch43 proves: deterministic quote w/ fixed clock; peak surcharge; Express/Overnight polymorphic
  surcharge; injected clock stamp; substituted tariff source; polymorphic selection by level;
  internal rules via InternalsVisibleTo; equivalence (seamed == refactored); legacy no-seam (reflection).
- ch44 proves: both engines deterministic + peak; equivalence (both satisfy IRateEngine); QuoteService
  works with either (one-line swap); substituted tariff drives both; internal peak-window via
  InternalsVisibleTo; legacy no stable seam + !IRateEngine (reflection).

Iteration: jekyll clean; diagrams.yml from #84 chapter order; test csprojs omit
TestingPlatformDotnetTestSupport + pin four packages; each `.slnx`; pure BCL;
InternalsVisibleTo wired; no cross-language token; primary names unchanged; AI-sidebar
component form; captions short; net10.0 + xUnit v3.

## Risks
- Legacy green AND untestable (HIGH): build.sh 0W/0E; member-scoped CA1822 only; reflection proof.
- idiomatic-review on legacy shape (HIGH): labelled; 0 on refactored.
- ch43 Feathers-intermediate `SeamedRateEngine` (MED): deliberately unimproved but idiomatic; all params used (no IDE0060).
- ch43 equivalence type mismatch (MED): compare `seamed.Quote(...)` decimal to `refactored.Quote(req).Amount`, same tariff+clock.
- No-cross-language at seam/catalogue/non-virtual (HIGH voice): positive+temporal; Wave D greps.
- Missing When-not-to-use / AI-sidebar form (MED): add; grep==1.
- Determinism (MED): injected IClock + FakeClock.
- Standing mechanics: `{% raw %}` for `{{`/`}}`; .slnx/global.json; CA1707 test-only; path leaks; pure BCL; NSubstitute test-only; InternalsVisibleTo csproj; #84 order; TODO-caption 0; word floor.

## r18 verification log

<!-- Opus fills this in at Wave D. -->
