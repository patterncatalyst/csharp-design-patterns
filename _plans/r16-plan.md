---
title: "r16 plan — Part 12 (ch39, ch40)"
output: false
render_with_liquid: false
---

# r16 — author ch39, ch40 (Part 12: Patterns in the Wild)

Opus-planned 2026-11-01. Branch `release/r16` (based on main@6acb306). Two "patterns in the wild"
chapters: shortcut patterns (a dispatch table that is a legitimate shortcut past a full pattern) and
anti-patterns / pattern misuse (over-engineering, including pattern-shaped LLM output). Standard net10.0
library + xUnit v3 toolchain (same as ch0-26/29-32). Research-heavy where claims need backing: the
authority is Microsoft Learn (`System.Collections.Frozen` / `FrozenDictionary<TKey,TValue>`, the
Framework Design Guidelines on simplicity) + the language spec + runnable experiments, NOT blog posts.
Four-wave shape (A code, 2 parallel agents / B diagrams AFTER green code with LOCKED names / C prose, 2
agents each voice-self-gating to 0 ban-tier / D validate). Enforces: idiomatic modern C# 14 ONLY — the
Part-13-only "old vs new" contrast exception does NOT apply here; NO cross-language comparison; keep
cross-chapter refs light (each chapter stands alone; name Strategy/Factory in place rather than pointing
to their chapters).

## Central constraint for ch40 — how to show an anti-pattern without older-idiom contrast

CLAUDE.md: code is idiomatic modern C# 14 only; the ONLY place "before/after" contrast code is allowed is
the Feathers/legacy part (Part 13), NOT here. ch40's subject is OVER-ABSTRACTION, which is an abstraction-
level problem, not a language-era problem. Resolution, locked:

- The RIGHT-SIZED form is the SHIPPED example: a tiny pure calculation (`RateCalculator.Quote`), clean
  C# 14, in the root namespace `FreightRating`. This is the artifact the chapter recommends.
- The OVER-ABSTRACTED form is ALSO real, compiling, idiomatic C# 14 (primary constructors, records,
  expression bodies — NOT "old C#") living in a clearly-labelled nested namespace
  `FreightRating.OverAbstracted`: a Strategy + Factory + interface tower that computes the SAME quote.
  BOTH compile green under warnings-as-errors.
- A [Theory] test proves the two forms are BEHAVIOURALLY EQUIVALENT over a spread of inputs, and the
  prose frames the collapsed form as the one to prefer. The anti-pattern is the abstraction LEVEL, shown
  by the two modern-C# forms sitting side by side, never by "modern vs retired idiom".
- `lgtm-idiomatic-review` detects reinvented framework features / deprecated APIs / non-idiomatic
  structure — it does NOT flag architectural altitude, so a cleanly-written over-abstracted tower passes
  0 hits. Wave A writes the tower in clean C# 14; Wave D runs the review and expects 0 hits (fix any
  surface idiom flagged while keeping the over-abstraction shape; the over-engineering is the point and
  is carried by prose + the `OverAbstracted` namespace label, not by any scannable idiom defect).

## Scope + example assignments (shipping domain, from the Notion per-chapter map)

- **ch39 Shortcut patterns** (`_docs/39-shortcut-patterns.md`, Documentation domain). A dispatch table
  for document types, backed by `FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>>`:
  a lookup/dispatch "shortcut" that is NOT an anti-pattern (a dictionary of handler delegates instead of a
  Strategy/Factory hierarchy, when that is the right-sized tool). Frame POSITIVELY: a table-driven
  dispatch is a legitimate, idiomatic shortcut past a full pattern when the variation is DATA, not
  behaviour that needs its own types. Pure BCL (`System.Collections.Frozen` is in-framework; see the ch20
  precedent). No package. Cite Microsoft Learn (`FrozenDictionary<TKey,TValue>`, `ToFrozenDictionary`,
  `System.Collections.Frozen`) primary; a LIGHT GoF Strategy/Factory cite (the patterns being shortcut).
- **ch40 Anti-patterns and pattern misuse** (`_docs/40-anti-patterns-and-pattern-misuse.md`, Rating
  domain). The over-abstracted rating engine: pattern misuse / over-engineering, INCLUDING pattern-shaped
  LLM output (an assistant emitting a Strategy+Factory+interface tower for a two-line calculation).
  Presented per the central constraint above: right-sized `RateCalculator.Quote` shipped, the
  over-abstracted tower in `FreightRating.OverAbstracted`, both green, a test proving equivalence, prose
  preferring the collapsed form. This chapter is the AI-throughline landing spot (pattern-shaped LLM
  output), but the single AI sidebar still goes in "When not to use it". Pure BCL. No package. Cite
  Microsoft Learn (Framework Design Guidelines: simplicity / avoiding over-design) primary; a LIGHT GoF
  Strategy / Abstract Factory cite (the patterns being misused).

## Confirmed facts

- Part slug `patterns-in-the-wild` (both stubs carry it in front matter; verified). Edit body in place.
- Branch `release/r16` already created off `main@6acb306` (verified HEAD = r15 merge). SDK pin 10.0.113,
  LangVersion 14.0, Nullable on, ImplicitUsings on, TreatWarningsAsErrors, AnalysisLevel
  latest-recommended, Deterministic — all inherited from `examples/Directory.Build.props`. Root
  `global.json` opts into `Microsoft.Testing.Platform`.
- Each stub's primary include is `{% include excalidraw.html name="<NN-...>" caption="Figure NN.1: TODO
  caption." %}`. Primary `name` MUST stay exactly `39-shortcut-patterns` and
  `40-anti-patterns-and-pattern-misuse` (verified). Replace ONLY the caption text, keeping the
  `Figure NN.1: ` prefix. `grep -c "TODO caption"` is 1 per stub today; MUST be 0 at the end.
- STUB SHAPE DIFFERS FROM r14: each stub has `## Learning objectives`, `## Why this matters`, the primary
  include, `## TODO: body`, `## Build, run, observe`, `## For deeper coverage`, and a leading
  `<!-- EXAMPLE (shipping domain): ... -->` comment. There is NO `## When not to use it` section yet.
  Wave C MUST replace `## TODO: body` with the real body sections AND ADD a `## When not to use it`
  section (holding exactly one AI-sidebar component) immediately before `## For deeper coverage`.
- AI sidebar = the NEW component form: a `<blockquote class="ai-sidebar" markdown="1">` ... `</blockquote>`
  wrapper, NO inline `**AI sidebar.**` lead-in (the CSS strip carries the "🤖 AI sidebar" label), and NOT
  the kramdown IAL `{: .ai-sidebar}` form (it breaks blockquote parsing). Gate:
  `grep -c '<blockquote class="ai-sidebar" markdown="1">'` == 1 per chapter.
- `_data/diagrams.yml` currently has **71** entries (verified via `grep -c "^- name:"`); append from
  **#72** in chapter order. ONE diagram agent owns diagrams.yml this iteration. Amber accent; light-fill
  emphasis boxes with dark text; no baked-in chapter numbers; ~960x560 C4-style; `section: "Ch. 39/40"`;
  SHORT captions (1-2 sentences, ~20-45 words, no angle-bracket generics).
- Template example layout (verified from `examples/22-mediator-and-chain-of-responsibility/`): library
  `.csproj` with `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**</DefaultItemExcludes>`; nested
  `*.Tests/` xUnit v3 csproj pinning the four standard packages (xunit.v3 4.0.2,
  xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0), setting
  `IsTestProject` true + `IsPackable` false + `NoWarn $(NoWarn);CA1707`, and OMITTING
  `TestingPlatformDotnetTestSupport`; a top-level `.slnx` listing both projects; a README mirroring
  ch22's. Everything else (net10.0, LangVersion, Nullable, ImplicitUsings, warnings-as-errors) inherited
  from `examples/Directory.Build.props`.
- FrozenDictionary precedent (verified in `examples/20-composite-and-flyweight/CommodityCodeRegistry.cs`
  + `PortCodeRegistry.cs`): `using System.Collections.Frozen;` + a `static readonly` seed array built
  once via `.ToFrozenDictionary(x => x.Key)` into a `private readonly FrozenDictionary<string,T>` field,
  with `Get` (throws a dedicated exception) + `TryGet` (non-throwing). ch39 mirrors this shape.
- Dependency survey: BOTH chapters are PURE BCL. `System.Collections.Frozen` / `ToFrozenDictionary` are
  in-framework (ch39). ch40 is plain BCL. NO runtime PackageReference in either library csproj. Test
  csprojs pin the four standard packages only.
- Namespace survey (verified): the two fresh namespaces do NOT collide:
  `DocumentDispatch` (ch39) and `FreightRating` + nested `FreightRating.OverAbstracted` (ch40).

## LOCKED type names (Wave A builds verbatim; Wave B diagrams copy from the green code)

### ch39 `examples/39-shortcut-patterns/` (namespace `DocumentDispatch`, pure BCL)
Project `DocumentDispatch.csproj`; tests `DocumentDispatch.Tests`; `DocumentDispatch.slnx`; README.
Library csproj adds NO runtime PackageReference (`System.Collections.Frozen` is in-framework). Add
`[assembly: InternalsVisibleTo("DocumentDispatch.Tests")]` (via a small `AssemblyInfo.cs` or csproj
`<InternalsVisibleTo>`) so the "built once / shared" test can see the static table.
- `public enum DocumentKind { BillOfLading, CommercialInvoice, PackingList, CertificateOfOrigin, CustomsDeclaration }`.
- `public sealed record DocumentRequest(DocumentKind Kind, string ConsignmentId, IReadOnlyDictionary<string, string> Fields);`
- `public sealed record RenderedDocument(DocumentKind Kind, string ConsignmentId, string Body);`
- `public static class DocumentRenderers` — one static method per kind, each `DocumentRequest -> RenderedDocument`:
  `RenderBillOfLading`, `RenderCommercialInvoice`, `RenderPackingList`, `RenderCertificateOfOrigin`,
  `RenderCustomsDeclaration`. Each stamps `Kind`, copies `ConsignmentId`, builds a distinct `Body` from
  `request.Fields`. Referenced as method groups into the table.
- `public sealed class UnknownDocumentKindException : Exception` — dedicated exception, standard three ctors.
- `public sealed class DocumentDispatcher`:
  - `internal static readonly FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>> Handlers`
    built ONCE at type init from a seed array via `.ToFrozenDictionary(...)`, each value a method group.
  - `public RenderedDocument Render(DocumentRequest request)` — one `Handlers.TryGetValue`; invoke or throw
    `UnknownDocumentKindException` naming the kind.
  - `public bool TryRender(DocumentRequest request, out RenderedDocument? document)` — non-throwing.
  - `public IReadOnlyCollection<DocumentKind> SupportedKinds => Handlers.Keys;`
  - `internal static FrozenDictionary<...> Table => Handlers;` (test can assert the single shared instance).
- **Tests** (`DocumentDispatch.Tests`, split `DispatchTests.cs` + `FrozenTableTests.cs`), ~9:
  1. `Render_dispatches_a_bill_of_lading_to_its_handler`.
  2. `Render_dispatches_each_document_kind_to_the_matching_handler` ([Theory] over every `DocumentKind`).
  3. `Render_preserves_the_consignment_id_on_the_rendered_document`.
  4. `Render_throws_for_an_unregistered_document_kind` (`(DocumentKind)999`).
  5. `TryRender_returns_true_and_the_document_for_a_registered_kind`.
  6. `TryRender_returns_false_and_null_for_an_unregistered_document_kind`.
  7. `Dispatch_table_has_a_handler_for_every_defined_document_kind` (reflection: `Enum.GetValues` == `Table.Keys`).
  8. `Dispatch_table_is_a_single_frozen_instance_shared_across_dispatchers`.
  9. `Dispatch_table_is_a_frozen_dictionary` (`Table is FrozenDictionary<...>`).

### ch40 `examples/40-anti-patterns-and-pattern-misuse/` (namespace `FreightRating` + nested `FreightRating.OverAbstracted`, pure BCL)
Project `FreightRating.csproj`; tests `FreightRating.Tests`; `FreightRating.slnx`; README. No runtime package.
- SHARED input (root `FreightRating`): `public sealed record RateRequest(decimal DeclaredValue, decimal WeightKilograms, bool FuelLevyApplies);`
- RIGHT-SIZED, SHIPPED (root `FreightRating`): `public static class RateCalculator` with
  `public static decimal Quote(RateRequest request)` — base = `DeclaredValue * 0.015m`; weight =
  `WeightKilograms * 2.50m`; levy = `FuelLevyApplies ? (base + weight) * 0.08m : 0m`; return the sum.
  Pure, no collaborators, no interfaces.
- OVER-ABSTRACTED, CAUTIONARY (nested `FreightRating.OverAbstracted`, clean C# 14):
  - `public interface IRateComponent { decimal Apply(RateRequest request, decimal runningSubtotal); }`
  - `public sealed class BaseRateComponent : IRateComponent` (`DeclaredValue * 0.015m`).
  - `public sealed class WeightSurchargeComponent : IRateComponent` (`WeightKilograms * 2.50m`).
  - `public sealed class FuelLevyComponent : IRateComponent` (`FuelLevyApplies ? runningSubtotal * 0.08m : 0m`).
  - `public interface IRateComponentFactory { IReadOnlyList<IRateComponent> CreateComponents(); }`
  - `public sealed class StandardRateComponentFactory : IRateComponentFactory` (base, weight, levy in order).
  - `public sealed class RateEngine(IRateComponentFactory factory)` with `public decimal Quote(RateRequest request)`
    folding the components, threading the running subtotal, returning the total.
- **Tests** (`FreightRating.Tests`, split `RateCalculatorTests.cs` + `EquivalenceTests.cs`), ~10:
  1. `Quote_sums_base_rate_and_weight_surcharge` (no levy).
  2. `Quote_adds_the_fuel_levy_when_it_applies`.
  3. `Quote_omits_the_fuel_levy_when_it_does_not_apply`.
  4. `Quote_returns_zero_for_an_empty_request`.
  5. `Quote_is_a_pure_calculation` (repeat-call stability).
  6. `Collapsed_calculator_exposes_a_single_public_quote_method` (reflection: public static methods count == 1).
  7. `Rate_engine_sums_its_components`.
  8. `Rate_engine_applies_the_fuel_levy_component_on_the_running_subtotal`.
  9. `Standard_factory_creates_the_three_rate_components_in_order`.
  10. `Both_forms_produce_the_same_quote` ([Theory] over a spread incl. levy on/off, zero, large:
      `RateCalculator.Quote(r)` == `new RateEngine(new StandardRateComponentFactory()).Quote(r)`).

## Waves

- **Wave A (code, 2 agents parallel — one per chapter):** build each example green.
  - Library csproj mirrors the template; NO runtime PackageReference (pure BCL). Nested `*.Tests/` pins the
    four standard packages, IsTestProject + IsPackable false + NoWarn CA1707, OMITS
    TestingPlatformDotnetTestSupport; each example its own `.slnx`; README mirroring ch22's.
  - ch39: `using System.Collections.Frozen;`; table built ONCE as `internal static readonly` via
    `.ToFrozenDictionary(...)` with method-group handlers; `[assembly: InternalsVisibleTo("DocumentDispatch.Tests")]`;
    Get/TryGet shape with `UnknownDocumentKindException`.
  - ch40: ship `RateCalculator.Quote` (right-sized) in root `FreightRating`; build the tower in nested
    `FreightRating.OverAbstracted`, clean idiomatic C# 14. BOTH green under warnings-as-errors. NO "old C#".
    Levy as a function of the running subtotal in BOTH forms so the equivalence test is exact.
  - Tests EXACTLY as listed (ch39 ~9, ch40 ~10); behaviour-asserting. Capture real path-sanitized
    `dotnet test` summary (0 `/home/`, 0 username). Wrap any fenced `{{`/`}}` in `{% raw %}` if it reaches a `.md`.
  - Each agent reports the real summary + the final LOCKED names built.

- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml from #72):** copy LOCKED real names.
  Paired SVG + `.excalidraw`, amber, light-fill emphasis boxes, no baked-in chapter numbers, ~960x560,
  `section: "Ch. 39/40"`, SHORT captions. Append from #72 in chapter order. 2 figures/chapter (+ density
  if >~2500, reconcile numbering with Wave C):
  - **#72 `39-shortcut-patterns`** (primary): the `FrozenDictionary` document dispatch table, `Render` one
    lookup instead of a class-per-case hierarchy.
  - **#73 `39-dispatch-table-vs-hierarchy`** (secondary): the data-driven table beside the
    Strategy/Factory hierarchy it stands in for; right-sized when variation is data.
  - **(#74 `39-frozen-table-built-once`, density if ch39 > ~2500):** seed array -> `ToFrozenDictionary` at
    type init, one shared read-optimized table.
  - **#75 `40-anti-patterns-and-pattern-misuse`** (primary): the over-abstracted tower (`IRateComponent` +
    `StandardRateComponentFactory` + `RateEngine`) beside `RateCalculator.Quote`, same quote.
  - **#76 `40-right-sizing-the-abstraction`** (secondary): collapsing the tower to one pure calculation.
  - **(#77 `40-pattern-shaped-llm-output`, density if ch40 > ~2500):** a model emitting Strategy+Factory
    scaffolding for what is really data, and the review collapsing it.
  Captions describe only real types; no cross-language tokens.

- **Wave C (prose, 2 agents, after code + figures):** write the full spine, REPLACE `## TODO: body`, ADD
  `## When not to use it` before `## For deeper coverage`, replace the TODO caption (keep `Figure NN.1: `).
  Spine: Learning objectives -> Why this matters -> primary FIGURE (after Why) -> body -> `## Build, run,
  observe` (real path-sanitized summary) -> `## When not to use it` (exactly ONE
  `<blockquote class="ai-sidebar" markdown="1">` ... `</blockquote>`, NO inline lead-in, NOT the IAL form)
  -> `## For deeper coverage` (Microsoft Learn primary; LIGHT GoF where genuinely in play).
  - ch39: frame the dispatch table POSITIVELY as a right-sized shortcut when variation is DATA; explain
    FrozenDictionary (built once, read-optimized, immutable), method-group handlers, Render/TryRender,
    table completeness, and when to graduate to a Strategy/Factory hierarchy. AI sidebar: a model reaching
    for a class-per-case tower where a data-driven table is right-sized.
  - ch40: teach pattern misuse via the two side-by-side MODERN forms, equivalence proven by tests, prefer
    the collapsed form; the anti-pattern is abstraction LEVEL, not language era (no "old C#"). AI sidebar
    (the one sidebar): pattern-shaped LLM output and the review that collapses it.
  - BOTH: zero em-dash; no rule-of-three; 0 ban-tier; no "capstone"; idiomatic C# 14 ONLY; NO cross-language;
    cross-chapter refs light; wrap `{{`/`}}` in `{% raw %}`. Body >= 2000 words, 2500 target; density figure
    (#74 / #77) if past ~2500.
  - **Voice self-gate (mandatory):** each prose agent runs `lgtm-professional-voice/scripts/scan.sh` on its
    own file to 0 ban-tier before handing back (avoid-list: genuinely/deliberately/earns-*/is worth
    its/for free/plainly/filler "actually"/on purpose/kickers/"capstone").

- **Wave D (validate):** idiomatic-review 0 hits both; voice 0 ban-tier both; em-dash 0 across docs + .cs +
  README; `grep -c "TODO caption"` == 0; `grep -ci capstone` == 0; AI-sidebar component grep == 1/chapter
  (and 0 inline `**AI sidebar.**` / 0 IAL `{: .ai-sidebar}`); caption gate (short, no `<...>` generics,
  `Figure NN.1: ` prefix); diagram name-drift gate; NO-CROSS-LANGUAGE grep (docs/.cs/README/new captions/new
  SVGs incl. Rust/Go/functional-language) == 0; cross-chapter gate; IDIOMATIC-ONLY gate (ch40: both forms
  modern, no older-idiom contrast); `dotnet test` per example (ch39 ~9, ch40 ~10) + `scripts/verify.sh`;
  `bundle exec jekyll build` clean (incl. the new `## When not to use it` sections); word counts; confirm
  diagrams.yml #72 chapter order, test csprojs omit TestingPlatformDotnetTestSupport + pin four packages,
  each `.slnx`, both pure BCL, `InternalsVisibleTo` wired. Write the verification log.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if > ~2500); full spine with the primary
figure AFTER "Why this matters"; `## When not to use it` added before `## For deeper coverage`; figure
pair(s) on disk + registered with matching `name` and REAL SHORT captions (no TODO); example `dotnet test`
green + in `scripts/verify.sh`; tests assert behaviour; "Build, run, observe" quotes real path-sanitized
output (0 `/home/`, 0 username); exactly one AI-sidebar component in "When not to use it"; zero em-dash
(docs + .cs + README); no rule-of-three; 0 ban-tier; no "capstone"; idiomatic C# 14 ONLY (no older-idiom
contrast); NO cross-language; cross-chapter refs light; no numeric cross-refs; every figure/caption type
exists in the example; primary citations Microsoft Learn with a light GoF cite for the pattern being
shortcut/misused.
- ch39 proves: `Render` dispatches each `DocumentKind` to the matching handler and preserves the
  consignment id; unregistered kind throws `UnknownDocumentKindException` while `TryRender` returns
  false/null; the table has a handler for every defined kind (completeness); the table is a single
  `FrozenDictionary` built once and shared across dispatchers.
- ch40 proves: `RateCalculator.Quote` sums base + weight, adds/omits the levy correctly, returns zero for
  an empty request, is pure; the `OverAbstracted` tower computes the same values; the equivalence [Theory]
  shows both forms agree; the collapsed calculator exposes a single public method. Both green; chapter
  prefers the collapsed form; contrast is abstraction level only.

Iteration: jekyll clean; diagrams.yml appended from #72 in chapter order; both test csprojs omit
TestingPlatformDotnetTestSupport + pin the four packages; each example own `.slnx`; both libraries pure
BCL; no cross-language token anywhere; primary figure names stay `39-shortcut-patterns` and
`40-anti-patterns-and-pattern-misuse`; AI sidebar uses the NEW component form (grep == 1/chapter);
captions short; standard net10.0 + xUnit v3 toolchain.

## Risks

- **ch40 over-abstraction vs the idiomatic-only rule + scanner (HIGH):** the tower must be CLEAN C# 14 at
  the idiom level so `lgtm-idiomatic-review` passes 0 hits; the over-engineering is architectural altitude,
  carried by prose + the `FreightRating.OverAbstracted` namespace, not a scannable idiom defect. NO
  "old C#"/"before" snippet (the Part-13 exception does NOT apply; Wave D greps for it). Levy a function of
  the running subtotal in BOTH forms so the equivalence [Theory] is exact.
- **ch39 FrozenDictionary idiom (MED):** table built ONCE as `internal static readonly` via
  `.ToFrozenDictionary(...)` (ch20 precedent), method-group handlers, dedicated `UnknownDocumentKindException`
  (3 ctors); `InternalsVisibleTo("DocumentDispatch.Tests")` for the shared-table test; unknown path uses
  `(DocumentKind)999`; `IReadOnlyDictionary`/`IReadOnlyList` on records (CA1819/CA2227).
- **AI-sidebar component form (MED):** `<blockquote class="ai-sidebar" markdown="1">`, no inline lead-in,
  NOT the IAL form; exactly one per chapter in "When not to use it".
- **Stub has no "When not to use it" section (MED, NEW this iter):** Wave C must add it before
  `## For deeper coverage`; Wave D confirms it holds exactly one AI-sidebar component.
- **Short captions / diagram name drift / TODO-caption / voice regression incl. capstone / word floor:**
  per the standing gates (r11-r15 lessons).
- **Standing mechanics:** Liquid `{{ }}` in fenced blocks wrapped in `{% raw %}`; `.slnx`/`global.json`;
  CA1707 test-only; path leaks; both libraries pure BCL; standard net10.0 + xUnit v3 toolchain.

## r16 verification log

Opus-validated 2026-11-01 (branch `release/r16`). Verdict: PASS (PASS-WITH-NITS at validation; both nits
fixed in a repair pass: ch39 figures renumbered to read 39.1 -> 39.2 -> 39.3 in flow after the density
figure landed mid-chapter; ch40's three generic "earlier chapters" leans reworded to name Strategy/
Factory Method in place). Part 12 "Patterns in the Wild" (ch39-40) is COMPLETE.

- Tests (live): `examples/39-shortcut-patterns` 13/13; `examples/40-anti-patterns-and-pattern-misuse`
  16/16; zero warnings under warnings-as-errors. `scripts/verify.sh` green (both auto-discovered, whole
  suite 31 projects). `scripts/build.sh` 0 Warning(s) 0 Error(s). jekyll clean.
- ch39: `FrozenDictionary<DocumentKind, Func<...>>` dispatch table built ONCE as `internal static
  readonly` via `ToFrozenDictionary`, method-group handlers, `Render`/`TryRender`, dedicated
  `UnknownDocumentKindException` (3 ctors), `InternalsVisibleTo("DocumentDispatch.Tests")`. Tests prove
  per-kind dispatch, completeness (`Enum.GetValues` == `Table.Keys`), single shared frozen instance.
- ch40: the right-sized `RateCalculator.Quote` shipped beside an over-abstracted Strategy+Factory tower
  in `FreightRating.OverAbstracted`, a `Both_forms_produce_the_same_quote` [Theory] proving equivalence.
  CRITICAL: BOTH forms are clean modern C# 14 (primary ctor, sealed records/classes, expression bodies,
  collection expressions) — the anti-pattern is abstraction LEVEL, carried by the `OverAbstracted`
  namespace + prose, NOT any older-idiom contrast; the Part-13 exception was not invoked.
  `lgtm-idiomatic-review` 0 hits on both.
- AI sidebar: both chapters use the NEW component (`<blockquote class="ai-sidebar" markdown="1">`,
  grep == 1/chapter, 0 inline `**AI sidebar.**`, 0 IAL), each in a `## When not to use it` section ADDED
  this iteration (the stubs lacked one). Rendered HTML emits `<blockquote class="ai-sidebar">` wrapping
  `<p>` (no `&gt;` parse failure). ch40's sidebar lands the pattern-shaped-LLM-output throughline.
- Captions SHORT on both surfaces (includes keep `Figure NN.x: ` prefix; diagrams.yml 1-2 sentences, no
  angle-bracket generics). Both chapters carry a density figure (39.2 frozen-table, 40.3 LLM-output).
  Six figures #72-77 on disk (.svg + .excalidraw), registered chapter-grouped; diagrams.yml 71 -> 77;
  name-drift clean.
- Voice 0 ban-tier both; 0 em-dash (docs + .cs + README); 0 "capstone"; 0 cross-language; no numeric
  cross-chapter refs. Body prose ~3490 (ch39) / ~2780 (ch40). Both libs pure BCL; test csprojs pin the
  four packages + IsTestProject + IsPackable false + NoWarn CA1707, omit TestingPlatformDotnetTestSupport;
  each example its own `.slnx`.

**Part 12 is COMPLETE (ch39-40).**
