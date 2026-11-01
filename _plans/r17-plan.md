---
title: "r17 plan — Part 13 (ch41, ch42)"
output: false
render_with_liquid: false
---

# r17 — author ch41, ch42 (Part 13: Changing Inherited Code, first half)

Opus-planned 2026-11-01. Branch `release/r17` (based on main@2a853b6, verified HEAD = r16 merge).
First two chapters of the legacy / Feathers track: seams in C# (why non-virtual-by-default makes a seam a
deliberate act) and the dependency-breaking catalogue for C# 14. Standard net10.0 library + xUnit v3
toolchain (same as ch0-26/29-32/39-40). ch43-44 are r18. Research authority is Michael Feathers, *Working
Effectively with Legacy Code* and Martin Fowler, *Refactoring* (2nd ed.) via the bibliography + publisher
links, plus Microsoft Learn for the C# mechanics (`protected virtual`, `sealed`, `InternalsVisibleTo`,
`TimeProvider`). Four-wave shape (A code, 2 parallel agents / B diagrams AFTER green code with LOCKED
names / C prose, 2 agents each voice-self-gating to 0 ban-tier / D validate).

## THE ONE SPECIAL RULE FOR THIS PART (Part 13 only) — before/after is ALLOWED here

CLAUDE.md permits classic-vs-refactored "before/after" contrast code ONLY in the legacy-code part, and
this is that part. Resolution, locked for both chapters:

- Each example ships BOTH a "legacy" (untestable-until-seamed) form AND a seam-enabled / dependency-broken
  refactor. The legacy form is realistic legacy STYLE: `sealed`, non-virtual methods, hard-coded `new` of
  a concrete collaborator, baked-in ambient statics (`DateTime.Now` / `DateTime.UtcNow` / `Guid.NewGuid` /
  `Environment.UserName` / a `static` tariff lookup), and NO seam. It is "hard to change / hard to test",
  NOT "wrong syntax": it COMPILES GREEN under net10.0 + warnings-as-errors + analyzers (latest-recommended).
- The legacy form lives in a clearly-labelled nested namespace `<Root>.Legacy` with `Legacy*` type names
  where it reads clearly, and every legacy type carries an XML-doc line stating it is an intentionally
  untestable legacy shape shown for contrast. This labels the shape so `lgtm-idiomatic-review` treats it as
  the deliberate subject, not a scannable idiom defect.
- The refactored form is clean, idiomatic modern C# 14 in the root namespace. It is the SHIPPED/TESTED
  artifact: the behaviour-asserting tests run ONLY against the refactored/seam-enabled form (the tests ARE
  the proof the seam / dependency-break worked). The "untestability" of the legacy form is made EXECUTABLE
  by a reflection test asserting the legacy type offers no seam (sealed, parameterless ctor only,
  non-virtual `Quote`), so the before/after contrast is deterministic and never relies on a flaky
  wall-clock assertion.
- `lgtm-idiomatic-review` must be 0 hits on the REFACTORED forms. On the `.Legacy` forms, findings are
  EXPECTED and INTENTIONAL (the `DateTime.Now` / baked-in `new` / static dependency are the chapter's
  subject); Wave A scopes them (the build is green; the review-skill findings are documented as intentional
  in the verification log, or scope-suppressed with a justification pointing to the Part-13 before/after
  exception). The point is testability/coupling, not a defect to repair.

## No-cross-language rule STILL HOLDS at the two tempting spots

"C# methods are non-virtual by default" and "classes are often sealed" are allowed as POSITIVE statements
about C#. NEVER contrast with Java/C++/JVM/Spring/Rust/Go or say "virtual-by-default language", "unlike
Java", "needs a workaround in X". Teach the seam and the dependency-breaking techniques as what C# does,
framed temporally (the seams Feathers catalogued, applied idiomatically in modern C# 14). Feathers and
Fowler are cited as the source works, not as a different-language contrast.

## Scope + example assignments (shipping Rating domain, self-contained per chapter)

Both chapters reuse "the rate engine" THEMATICALLY, but each example is SELF-CONTAINED in its own
`examples/NN-*/` dir with a FRESH namespace and its own project. ch42 may say "the same engine"
generically but stands alone (no ch3/ch40 number/title lean).

- **ch41 Seams in C#: non-virtual by default** (`_docs/41-seams-non-virtual-by-default.md`, Rating domain,
  `examples/41-seams-non-virtual-by-default/`, namespace `RateEngineSeams` + nested `RateEngineSeams.Legacy`).
  A legacy `RateEngine` that is `sealed`, non-virtual, with a hard-coded `new` tariff table and a
  `DateTime.Now` peak surcharge, so it cannot be driven deterministically or in isolation. Then introduce
  the C# seams that get it under test without rewriting the behaviour: extract an interface + constructor
  injection (object seam), wrap the ambient clock behind an injected `IClock` (the static-dependency seam;
  prose names `TimeProvider` as the BCL version of exactly this wrapper, kept package-free), a
  `protected virtual` extract-and-override hook (virtual as a deliberate act, which means the refactored
  class is intentionally NOT sealed), and `InternalsVisibleTo` for an internal seam. Teaching point: a seam
  in C# is a deliberate act because methods are non-virtual by default and classes are frequently sealed.
  Pure BCL; NSubstitute (test-only) for one object-seam substitution. NO MEDI.
- **ch42 Dependency-breaking techniques for C# 14** (`_docs/42-dependency-breaking-techniques.md`, Rating
  domain, `examples/42-dependency-breaking-techniques/`, namespace `RateEngineDependencies` + nested
  `RateEngineDependencies.Legacy`). The SAME engine theme with baked-in dependencies: a `new` concrete
  audit log, a `static` tariff book, `Guid.NewGuid()` references, `DateTime.UtcNow`, and
  `Environment.UserName`. Apply the Feathers dependency-breaking catalogue idiomatically in C# 14: Extract
  Interface, Parameterize Constructor, Parameterize Method, Extract and Override Call (the `protected
  virtual` hook), `InternalsVisibleTo` for an internal seam, and wrapping ambient dependencies (clock +
  reference generator) behind injected abstractions. An NSubstitute fake then drives the dependency-broken
  engine and VERIFIES the audit interaction the legacy form hid. Pure BCL; NSubstitute (test-only). NO MEDI.

## Confirmed facts

- Part slug `changing-inherited-code` (both stubs carry it; verified). Edit body in place.
- Branch `release/r17` created off `main@2a853b6`. SDK pin 10.0.113, LangVersion 14.0, Nullable on,
  ImplicitUsings on, TreatWarningsAsErrors, AnalysisLevel latest-recommended, Deterministic — inherited
  from `examples/Directory.Build.props`. Root `global.json` opts into `Microsoft.Testing.Platform`.
- Each stub's primary include is `{% include excalidraw.html name="<NN-...>" caption="Figure NN.1: TODO
  caption." %}`. Primary `name` MUST stay exactly `41-seams-non-virtual-by-default` and
  `42-dependency-breaking-techniques`. Replace ONLY the caption text, keeping the `Figure NN.1: ` prefix.
  `grep -c "TODO caption"` is 1 per stub today; MUST be 0 at the end.
- STUB SHAPE (same as r16): each stub has `## Learning objectives`, `## Why this matters`, the primary
  include, `## TODO: body`, `## Build, run, observe`, `## For deeper coverage`, and a leading
  `<!-- EXAMPLE ... -->` comment. There is NO `## When not to use it` section yet. Wave C MUST replace
  `## TODO: body` with the real body AND ADD a `## When not to use it` section (holding exactly one
  AI-sidebar component) immediately before `## For deeper coverage`.
- AI sidebar = the NEW component form: `<blockquote class="ai-sidebar" markdown="1"> ... </blockquote>`,
  NO inline `**AI sidebar.**` lead-in, NOT the kramdown IAL `{: .ai-sidebar}` form. Gate:
  `grep -c '<blockquote class="ai-sidebar" markdown="1">'` == 1 per chapter.
- `_data/diagrams.yml` currently has **77** entries (last `40-pattern-shaped-llm-output`); append from
  **#78** in chapter order. ONE diagram agent owns diagrams.yml. Amber accent; light-fill emphasis boxes;
  no baked-in chapter numbers; ~960x560 C4-style; `section: "Ch. 41/42"`; SHORT captions.
- Template layout from `examples/22-mediator-and-chain-of-responsibility/`. InternalsVisibleTo precedent:
  `examples/39-shortcut-patterns/DocumentDispatch.csproj` (csproj `<InternalsVisibleTo Include=".Tests"/>`).
  Clock-injection precedent: `examples/32-tap-and-async-conventions/FakeCarrierGateway.cs` (ctor-injected
  time). NSubstitute idiom: `examples/10-testing-with-xunit-and-nsubstitute/`.
- Dependency survey: BOTH chapters pure BCL in the library csproj (no runtime PackageReference). `IClock`
  hand-rolled; `TimeProvider` (if named in prose) in-framework. NSubstitute test-only (one of the four
  standard test pins). NO MEDI.
- Bibliography: Feathers, *Working Effectively with Legacy Code*
  (https://www.oreilly.com/library/view/working-effectively-with/0131177052/); Fowler, *Refactoring*
  (2nd ed.) (https://martinfowler.com/books/refactoring.html). Cite via the bibliography page + publisher
  links; Microsoft Learn for the C# mechanics.
- Fresh namespaces (confirmed non-colliding): `RateEngineSeams` (+ `.Legacy`), `RateEngineDependencies`
  (+ `.Legacy`).

## LOCKED type names (Wave A builds verbatim; Wave B diagrams copy from the green code)

### ch41 `examples/41-seams-non-virtual-by-default/` (namespace `RateEngineSeams` + nested `.Legacy`, pure BCL)
Project `RateEngineSeams.csproj`; tests `RateEngineSeams.Tests`; `RateEngineSeams.slnx`; README. Library
csproj: NO runtime PackageReference; add `<InternalsVisibleTo Include="RateEngineSeams.Tests" />`.

Shared domain (root `RateEngineSeams`):
- `public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms);`
- `public sealed record RateQuote(string ConsignmentId, decimal Amount, DateTimeOffset QuotedAt);`

Legacy, untestable (nested `RateEngineSeams.Legacy`; XML-doc labelled intentional):
- `public sealed class LegacyTariffTable` — instance dictionary seeded in ctor; `public decimal
  RatePerKilogram(string laneCode)` (uses instance state so CA1822 stays quiet).
- `public sealed class RateEngine` — untestable. `public decimal Quote(RateRequest request)`:
  `var tariff = new LegacyTariffTable();` (hard-coded new); base `DeclaredValue * 0.015m`; weight
  `WeightKilograms * tariff.RatePerKilogram("STD")`; `if (DateTime.Now.Hour is >= 8 and < 18) subtotal *= 1.1m;`
  (ambient clock); return subtotal. Sealed, non-virtual, parameterless.

Refactored, seam-enabled, SHIPPED/TESTED (root `RateEngineSeams`):
- `public interface ITariffSource { decimal RatePerKilogram(string laneCode); }` + `public sealed class
  StandardTariffSource : ITariffSource`.
- `public interface IClock { DateTimeOffset UtcNow { get; } }` + `public sealed class SystemClock : IClock`
  (`=> DateTimeOffset.UtcNow`).
- `internal static class PeakWindow { internal static bool IsPeak(DateTimeOffset at) => at.Hour is >= 8 and < 18; }`
- `public class RateEngine(ITariffSource tariffs, IClock clock)` — NOT sealed:
  - `public RateQuote Quote(RateRequest request)`: base + weight via `tariffs`; `var at = clock.UtcNow;`
    surcharge via `PeakSurcharge(subtotal, at)`; `new RateQuote(request.ConsignmentId, total, at)`.
  - `protected virtual decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) => PeakWindow.IsPeak(at) ? subtotal * 0.1m : 0m;`

Test doubles (in `RateEngineSeams.Tests`): `public sealed class FakeClock(DateTimeOffset utcNow) : IClock`;
`public sealed class TestableRateEngine : RateEngine` overriding `PeakSurcharge`; NSubstitute
`Substitute.For<ITariffSource>()`.

**Tests** (`RateEngineSeams.Tests`, split `SeamTests.cs` + `LegacyContrastTests.cs`), 9:
1. `Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock` (off-peak).
2. `Refactored_engine_applies_the_peak_surcharge_during_the_peak_window` (09:00).
3. `Refactored_engine_omits_the_peak_surcharge_outside_the_peak_window` (23:00).
4. `Refactored_engine_stamps_the_quote_with_the_injected_clock`.
5. `Injected_tariff_source_can_be_substituted` (NSubstitute).
6. `Extract_and_override_seam_lets_a_subclass_replace_the_surcharge` (`TestableRateEngine`).
7. `Internal_peak_window_is_reachable_through_internals_visible_to` ([Theory]).
8. `Legacy_engine_offers_no_seam_for_the_clock_or_the_tariff_source` (reflection: sealed, parameterless ctor, non-virtual `Quote`).
9. `Standard_tariff_source_returns_the_expected_rate`.

### ch42 `examples/42-dependency-breaking-techniques/` (namespace `RateEngineDependencies` + nested `.Legacy`, pure BCL)
Project `RateEngineDependencies.csproj`; tests `RateEngineDependencies.Tests`; `RateEngineDependencies.slnx`;
README. Library csproj: NO runtime PackageReference; add `<InternalsVisibleTo Include="RateEngineDependencies.Tests" />`.

Shared domain (root `RateEngineDependencies`):
- `public sealed record RateRequest(string ConsignmentId, decimal DeclaredValue, decimal WeightKilograms, string LaneCode);`
- `public sealed record RateQuote(string Reference, string ConsignmentId, decimal Amount, string QuotedBy, DateTimeOffset QuotedAt);`

Legacy, baked-in dependencies (nested `.Legacy`; XML-doc labelled):
- `public static class LegacyTariffBook { public static decimal RateFor(string laneCode) => ...; }` (static dependency).
- `public sealed class LegacyAuditLog` — instance `List<string>`, `public void Record(string message)`.
- `public sealed class RateEngine` — untestable. `public RateQuote Quote(RateRequest request)`:
  `var audit = new LegacyAuditLog();`; `var rate = LegacyTariffBook.RateFor(request.LaneCode);`;
  `var reference = Guid.NewGuid().ToString("N");`; `var quotedAt = DateTime.UtcNow;`;
  `var quotedBy = Environment.UserName;` (read at runtime only — never quoted into docs/output, no host leak);
  compute amount, `audit.Record(...)`, return `RateQuote`. Sealed, non-virtual, parameterless.

Refactored, dependency-broken, SHIPPED/TESTED (root `RateEngineDependencies`):
- `public interface ITariffBook { decimal RateFor(string laneCode); }` + `public sealed class StandardTariffBook : ITariffBook`.
- `public interface IAuditLog { void Record(string message); }` + `public sealed class ListAuditLog : IAuditLog` (instance `List<string>` + `IReadOnlyList<string> Entries`).
- `public interface IClock { DateTimeOffset UtcNow { get; } }` + `public sealed class SystemClock : IClock`.
- `public interface IReferenceGenerator { string NewReference(); }` + `public sealed class GuidReferenceGenerator : IReferenceGenerator`.
- `internal static class LaneValidation { internal static bool IsKnownLane(string laneCode) => ...; }`
- `public class RateEngine(ITariffBook tariffs, IAuditLog audit, IClock clock, IReferenceGenerator references)` — NOT sealed:
  - `public RateQuote Quote(RateRequest request, string quotedBy)` (Parameterize Method replaces
    `Environment.UserName`). Validate lane via `LaneValidation.IsKnownLane` (throw `UnknownLaneException`);
    amount from `tariffs.RateFor`; `var reference = NewReference();` `var at = clock.UtcNow;`
    `audit.Record(...)`; return `RateQuote`.
  - `protected virtual string NewReference() => references.NewReference();` (Extract-and-Override).
- `public sealed class UnknownLaneException : Exception` (standard three ctors).

Test doubles (in `RateEngineDependencies.Tests`): NSubstitute `Substitute.For<...>()` for each interface
(`.Returns` queries, `.Received(1)` commands, `Arg`); `public sealed class FixedReferenceEngine : RateEngine`
overriding `NewReference()`.

**Tests** (`RateEngineDependencies.Tests`, split `DependencyBreakingTests.cs` + `LegacyContrastTests.cs`), 10:
1. `Quote_uses_the_injected_tariff_book_rate`.
2. `Quote_records_an_audit_entry_for_the_quote` (`audit.Received(1).Record(...)`).
3. `Quote_stamps_the_injected_clock_time`.
4. `Quote_uses_the_injected_reference_generator`.
5. `Quote_carries_the_parameterized_quoted_by_value`.
6. `Quote_rejects_an_unknown_lane` (`UnknownLaneException`).
7. `Lane_validation_is_reachable_through_internals_visible_to` ([Theory]).
8. `Extract_and_override_seam_replaces_the_reference_generator` (`FixedReferenceEngine`).
9. `Legacy_engine_bakes_in_its_dependencies_with_no_seam` (reflection: sealed, parameterless ctor, non-virtual; `LegacyTariffBook` static).
10. `Standard_tariff_book_and_guid_reference_generator_behave`.

## Waves

- **Wave A (code, 2 agents parallel):** build each example green. Library csproj mirrors the template; NO
  runtime PackageReference; add the csproj `<InternalsVisibleTo Include="<Project>.Tests" />`. Nested
  `*.Tests/` pins the four standard packages, IsTestProject + IsPackable false + NoWarn CA1707, OMITS
  TestingPlatformDotnetTestSupport; each example its own `.slnx`; README mirroring ch22's. Build BOTH forms;
  CONFIRM the legacy form compiles GREEN under warnings-as-errors + analyzers (watch CA1822 on legacy
  instance methods; if any CA/IDE rule fires on the ambient statics, scope-suppress on the legacy member
  with a Part-13 justification and RECORD it). Tests EXACTLY as listed (ch41 9, ch42 10); legacy "no seam"
  proof is a reflection test; ch42 drives fakes with NSubstitute. Capture the real path-sanitized
  `dotnet test` summary. Each agent reports the summary, the final LOCKED names, and the legacy-form
  analyzer outcome.
- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml from #78):** LOCKED names, SHORT
  captions, `section: "Ch. 41/42"`, no cross-language / no "virtual-by-default language" in labels. Append
  from #78 chapter order. 2 figures/chapter (+ density if >~2500):
  - **#78 `41-seams-non-virtual-by-default`** (primary): legacy sealed/non-virtual `RateEngine` (baked-in
    tariff + `DateTime.Now`) beside the refactored engine taking an injected tariff source + clock with a
    `protected virtual` hook.
  - **#79 `41-c-sharp-seam-kinds`** (secondary): the four seams — extract interface + ctor injection, wrap
    the ambient clock, `protected virtual` extract-and-override, internal seam via `InternalsVisibleTo`.
  - **(#80 `41-seam-enabled-testing`, density if >~2500):** a test driving the refactored engine through the
    opened seams where the legacy form admitted none.
  - **#81 `42-dependency-breaking-techniques`** (primary): legacy engine with baked-in `new` audit log,
    static tariff book, ambient reference/time/user beside the dependency-broken engine with each
    collaborator injected.
  - **#82 `42-feathers-catalogue-in-c-sharp`** (secondary): Extract Interface, Parameterize Constructor,
    Parameterize Method, Extract and Override, `InternalsVisibleTo`, wrap an ambient dependency.
  - **(#83 `42-fake-drives-the-engine`, density if >~2500):** an NSubstitute fake driving the engine and
    verifying the audit interaction the legacy form hid.
- **Wave C (prose, 2 agents):** full spine, REPLACE `## TODO: body`, ADD `## When not to use it` before
  `## For deeper coverage`, replace the TODO caption (keep `Figure NN.1: `). Spine: Learning objectives ->
  Why this matters -> primary FIGURE -> body -> `## Build, run, observe` (real path-sanitized summary) ->
  `## When not to use it` (exactly ONE `<blockquote class="ai-sidebar" markdown="1">`, no inline lead-in,
  not IAL) -> `## For deeper coverage` (Feathers + Fowler via bibliography + publisher links; Microsoft
  Learn for the C# mechanics). ch41: seam as a deliberate act in C# (non-virtual by default, often sealed —
  positive only), walk the legacy form then open each seam, name `TimeProvider` as the BCL clock wrapper.
  ch42: the Feathers dependency-breaking catalogue in C# 14, NSubstitute fake drives the result; "the same
  engine" generic. BOTH: before/after is the labelled subject; zero em-dash; no rule-of-three; 0 ban-tier;
  no "capstone"; NO cross-language (no Java/C++/virtual-by-default contrast); cross-chapter refs light; wrap
  `{{`/`}}` in `{% raw %}`; body >= 2000, 2500 target; density figure (#80/#83) if past ~2500. Voice
  self-gate to 0 ban-tier before handback.
- **Wave D (validate):** idiomatic-review 0 hits on REFACTORED forms (legacy findings intentional/documented);
  voice 0 ban-tier both; em-dash 0 across docs + .cs + README; `grep -c "TODO caption"` == 0; `grep -ci
  capstone` == 0; AI-sidebar component grep == 1/chapter (0 inline lead-in, 0 IAL) in the ADDED When-not-to-use
  section; caption gate (short, no `<...>` generics, prefix); diagram name-drift gate; NO-CROSS-LANGUAGE grep
  (incl. `virtual.by.default`, Java/C++/JVM/Spring/Rust/Go) == 0; cross-chapter gate; legacy-label gate
  (`.Legacy` namespace + `Legacy*` + XML-doc intentional, compiles green); `dotnet test` per example (ch41 9,
  ch42 10) + `scripts/verify.sh` + `scripts/build.sh` (0W/0E confirms both legacy forms compile); jekyll
  clean; word counts; diagrams.yml #78 chapter order; test csprojs omit TestingPlatformDotnetTestSupport +
  pin four packages; each `.slnx`; both pure BCL; `InternalsVisibleTo` wired. Write the verification log.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if >~2500); full spine with the primary figure
AFTER "Why this matters"; `## When not to use it` ADDED before `## For deeper coverage`; figure pair(s) on
disk + registered with matching `name` and REAL SHORT captions (no TODO); example `dotnet test` green + in
`scripts/verify.sh`; tests assert behaviour; BOTH legacy + refactored forms compile green; "Build, run,
observe" quotes real path-sanitized output (0 `/home/`, 0 username); exactly one AI-sidebar component; zero
em-dash (docs + .cs + README); no rule-of-three; 0 ban-tier; no "capstone"; NO cross-language (no
Java/C++/virtual-by-default contrast); cross-chapter refs light; no numeric cross-refs; every figure/caption
type exists in the example; legacy form clearly labelled (`.Legacy` / `Legacy*`); citations Feathers + Fowler
via bibliography + publisher links + Microsoft Learn for the mechanics.
- ch41 proves: refactored engine quotes deterministically with a fixed clock, applies/omits the peak
  surcharge inside/outside the window, stamps the injected clock, accepts a substituted tariff source,
  accepts a subclass surcharge override, exposes peak-window logic through `InternalsVisibleTo`; the legacy
  engine offers NO seam (reflection). Seam shown as a deliberate act in C#.
- ch42 proves: dependency-broken engine uses the injected tariff book, records an audit entry a fake can
  VERIFY, stamps the injected clock, uses the injected reference generator, carries a parameterized
  `quotedBy`, rejects an unknown lane, exposes lane validation through `InternalsVisibleTo`, accepts a
  subclass reference-call override; the legacy engine bakes in its dependencies with no seam (reflection).
  An NSubstitute fake drives the engine.

Iteration: jekyll clean; diagrams.yml appended from #78 in chapter order; both test csprojs omit
TestingPlatformDotnetTestSupport + pin the four packages; each example own `.slnx`; both libraries pure BCL;
`InternalsVisibleTo` wired; no cross-language token anywhere; primary figure names stay
`41-seams-non-virtual-by-default` and `42-dependency-breaking-techniques`; AI sidebar uses the NEW component
form; captions short; standard net10.0 + xUnit v3 toolchain.

## Risks

- **Legacy form must compile green AND stay realistically "untestable" (HIGH):** realistic legacy STYLE that
  still passes warnings-as-errors + analyzers. Watch CA1822 (keep legacy instance methods using instance
  state), ambient statics (none expected to trip under latest-recommended; Wave A confirms, scope-suppresses
  with a Part-13 justification only if one fires). The "untestable" claim is EXECUTABLE via a reflection "no
  seam" test, never a flaky wall-clock assertion.
- **idiomatic-review flagging the deliberately-legacy shape (HIGH):** label it (`.Legacy` namespace +
  `Legacy*` names + XML-doc intentional note). Refactored forms 0 hits; legacy-form findings documented as
  intentional (the Part-13 subject), not repaired.
- **No-cross-language temptation at "non-virtual by default" / "seam" (HIGH voice risk):** positive + temporal
  only; no Java/C++/virtual-by-default contrast. Wave D greps docs, code, README, captions, SVGs.
- **AI-sidebar component form (MED); missing When-not-to-use section (MED); clock/ambient determinism (MED):**
  per the plan; refactored engines take injected `IClock`/reference generator so every test is deterministic;
  `Environment.UserName` parameterized, never printed.
- **Short captions / diagram name drift / TODO-caption / voice regression incl. capstone / word floor:** per
  the standing r11-r16 gates.
- **Standing mechanics:** Liquid `{{ }}` in fenced blocks wrapped in `{% raw %}`; `.slnx`/`global.json`;
  CA1707 test-only; path leaks; both libraries pure BCL; NSubstitute test-only; `InternalsVisibleTo` via
  csproj; net10.0 + xUnit v3.

## r17 verification log

Adversarial validation 2026-11-01 (Opus, branch `release/r17`). Verdict: **PASS**.
Every criterion read/run against actual files and commands; no prior self-report trusted.

### Build / test
- **ch41 `dotnet test --solution RateEngineSeams.slnx`** — MET. total 13, failed 0,
  succeeded 13 (both legacy + refactored forms compiled under TreatWarningsAsErrors).
- **ch42 `dotnet test --solution RateEngineDependencies.slnx`** — MET. total 14, failed 0,
  succeeded 14.
- **`scripts/build.sh`** — MET. Build succeeded, 0 Warning(s), 0 Error(s) across all examples
  (both legacy forms compile green under warnings-as-errors + analyzers).
- **`scripts/verify.sh`** — MET. All suites green; both new examples auto-discovered via the
  `examples/*/*.Tests/*.csproj` glob (ch41 13/0, ch42 14/0 present in the run).
- **`bundle exec jekyll build`** — MET. Clean build, exit 0, done in 0.431s, no Liquid errors.
  (commonmarker/prism "Ignoring … missing extensions" are unrelated gem notices, not build
  errors.)

### Idiomatic review (refactored forms)
- **lgtm-idiomatic-review scan** — MET. 0 hits on both example trees (refactored and legacy
  alike; target was 0 on refactored forms). Refactored forms use primary constructors, records,
  interface seams, nullable-on, no reinvented BCL.

### Voice + prose
- **professional-voice scan** — MET. 0 ban-tier both. Watch-tier: ch41 8 lines (hedging ×4,
  actually ×2, simply/obviously ×1, section-covers ×1); ch42 17 lines (actually ×4, defect
  framing ×4, hedging ×3, deliberate-adj ×2, soft hype ×2, quietly/silently ×1, security-finding
  framing ×1). Watch-tier only, acceptable.
- **em-dash** — MET. 0 across both docs, all new .cs, both READMEs.
- **capstone** — MET. 0 both docs.
- **No cross-language** — MET. 0 genuine hits for java / c++ / jvm / spring / rust / golang /
  bare `go` across docs, new .cs, READMEs, 5 SVGs, 5 yml captions. `virtual.by.default` matches
  are only the chapter slug/path and the allowed positive title "Seams in C#: non-virtual by
  default" (regex `.` matched the `-`); prose frames C# positively, no language contrast.
- **Cross-chapter** — MET. 0 numeric or title/topic cross-references in either doc.

### Structure
- **Spine order** — MET both. Learning objectives → Why this matters → primary figure (ch41 L28,
  ch42 L67, after Why) → body → Build, run, observe → When not to use it → For deeper coverage.
- **When not to use it** — MET both. Present, immediately before For deeper coverage; exactly one
  `<blockquote class="ai-sidebar" markdown="1">` each; 0 inline `**AI sidebar.**` lead-in; 0
  kramdown IAL.
- **TODO caption** — MET. 0 both.
- **Captions** — MET. All 5 keep `Figure NN.N:` prefix, 1-2 sentences ~22-33 words, no
  angle-bracket generics, zero em-dash, human-readable glosses (not identifier inventories).
- **Figure/diagram resolution** — MET. ch41 embeds 41-seams-non-virtual-by-default +
  41-c-sharp-seam-kinds; ch42 embeds 42-dependency-breaking-techniques +
  42-feathers-catalogue-in-c-sharp + 42-fake-drives-the-engine. All resolve to .svg + .excalidraw
  on disk and a diagrams.yml entry. diagrams.yml = 82 entries; 5 new in chapter order (41s before
  42s), sections "Ch. 41" / "Ch. 42".
- **Caption types exist** — MET. RateEngine, IAuditLog, audit.Received(1).Record all present.

### Toolchain
- **Library csprojs** — MET both. Zero runtime PackageReference (pure BCL); each has
  `<InternalsVisibleTo Include="<Project>.Tests" />`.
- **Test csprojs** — MET both. Pin exactly xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1,
  Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0; IsTestProject + IsPackable false + NoWarn
  CA1707; no TestingPlatformDotnetTestSupport.
- **Each example has its own .slnx** — MET (RateEngineSeams.slnx, RateEngineDependencies.slnx).
- **Path sanitization** — MET. 0 `/home/` and 0 real username in both docs' Build/run/observe.

### Part-13 legacy labelling
- **Legacy forms labelled** — MET. All legacy types in `.Legacy` namespaces with `Legacy*` names
  (LegacyTariffTable, LegacyAuditLog, LegacyTariffBook; engines are `Legacy.RateEngine`), each
  carrying an XML-doc "Intentionally untestable legacy shape shown for contrast" line.
- **CA1822 suppression member-scoped** — MET. Exactly two `[SuppressMessage(... CA1822 ...)]`, both
  on the legacy `RateEngine.Quote` members with a Part-13 before/after justification. No
  assembly-level suppression, no GlobalSuppressions.cs, no example-level .editorconfig.
- **idiomatic-review legacy hits** — none fired (scan 0); the expected legacy findings
  (hard-coded `new`, `DateTime.Now`/`UtcNow`, static dependency, `Environment.UserName`) are the
  documented chapter subject, not defects.

### Word counts
- ch41 body prose ≥ 2000 — MET (~3050 words). ch42 — MET (~3457 words; plan estimate ~3286, the
  delta is counting methodology — headings/list markers included — not a shortfall).

### Nits (non-blocking)
- None requiring repair. Watch-tier voice counts (esp. ch42's "actually" ×4 and "defect framing"
  ×4) are within tolerance; no action required.

