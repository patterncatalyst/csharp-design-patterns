---
title: "r24 plan — Appendices E, F, G"
output: false
render_with_liquid: false
---

# r24 — author Appendices E, F, G (final book prose)

Opus-planned 2026-11-01. Branch `release/r24` (based on main@6311cc2, r23 merge).
All 47 chapters (0-46) + Appendices A-D are merged. This batch COMPLETES all book
prose; only the four companion decks (101/201/301/401) remain after it. Reference-page
shape (r23 precedent): sections = Learning objectives + Why this matters + body +
For-deeper-coverage ONLY. NO example, NO diagram, NO Build-run-observe, NO
When-not-to-use, NO AI-sidebar (grep==0 each). net10.0/C#14 scope EXCEPT G (the one
additive C# 15 / .NET 11 exception). diagrams.yml stays 112 (no figures).

## Confirmed facts
- Part slug `appendices`; E/F/G = `_docs/51/52/53`. Stubs carry front matter + the 4-section skeleton; keep front matter + section names; replace `## TODO: body`; remove all TODO comments.
- Bibliography rows ALL LIVE (no new rows): GoF (E + F composition), Martin PPP/SOLID (E + F), Pragmatic Programmer / Clean Code / Code Complete / Mythical Man-Month (F). YAGNI/KISS/Demeter/fail-fast/composition have NO dedicated row → attribute to the live rows (Demeter/DRY/orthogonality→Pragmatic; KISS/simplicity→Code Complete; do-one-thing/Boy-Scout→Clean Code; composition→GoF). Do NOT invent rows.
- **F finding (verified by grep across all 47 chapters): the principle vocabulary is almost entirely ABSENT by name** (only "single responsibility" appears, in ch05). F is ADDITIVE vocabulary — anchor each principle to where it is EMBODIED in a real example/pattern, NEVER claim the chapters name it. Wave-D F gate = embodiment-accuracy, not name-matching.
- Reference cross-ref relaxation: name patterns/idioms + real `examples/<dir>`; avoid bare "chapter NN" numeric pointers.

## G research block (VERIFIED 2026-11-01 w/ sources — Wave C RE-VERIFIES at authoring)
**.NET 11 / C# 15 are RELEASE CANDIDATE as of 2026-11-01; GA scheduled 2026-11-10 (STS, 2-yr support to 2028-11-09).** G frames the whole appendix RC-today/GA-2026-11-10, dated, additive (chapters stay C#14), every claim cited to Microsoft Learn.
- Sources: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview · https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15 · https://github.com/dotnet/core/blob/main/release-notes/11.0/README.md · https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- C# 15 confirmed features (GA at .NET 11 GA unless noted): (1) collection expression arguments `with(...)`; (2) **union types** (`union` keyword, exhaustive switch, runtime `IUnion`/`UnionAttribute`); (3) **closed hierarchies** (`closed` modifier → exhaustive switch no default); (4) extension indexers; (5) labeled `break`/`continue`; (6) non-virtual static interface members on older runtimes; (7) **memory safety — PREVIEW ONLY** (LangVersion preview + `updated-memory-safety-rules` flag; `safe` keyword, `unsafe(expr)`) — G labels this explicitly preview/unsettled.
- .NET 11 deltas touching the book: **Runtime-native async** (GA; no EnablePreviewFeatures on net11.0; cleaner traces/lower overhead — IMPLEMENTATION change, NOT a model change); LINQ FullJoin/tuple joins + collection-expression args; System.Text.Json union support; async options validation (`IAsyncStartupValidator`/`AsyncValidationAttribute`); numeric (Decimal32/64/128, generic `Complex<T>`, `Random.NextInteger<T>`); xUnit v3 on MTP as the default test template (confirms the book's existing choice).
- Feature→chapter map: runtime-async→ch33/ch32/Part11; unions→ch31 Result/ch24 Visitor; closed→ch13 State/ch24; extension indexers→ch25; static interface members+numerics→ch29; async options→ch30; LINQ joins+collection-expr args→ch23/ch08; labeled break/continue→general (minor); memory safety [PREVIEW]→ch08; xUnit-v3-MTP default→App B/ch10 (confirms, no change).

## Approach
All three: LO + Why + body + For-deeper-coverage; no example/diagram/sidebar; keep front matter + section names.
- **E Pattern-to-force quick reference** (`_docs/51`): ~150-300w framing prose (what "forces" means, how to read, temporal thesis) + one big accurate markdown TABLE. Columns: `Pattern / principle / idiom` | `Where it appears` (named area/pattern, NOT bare chapter number) | `Forces it answers` | `Modern C# note` (the C#14/.NET10 idiom answering the force). One row per pattern/principle/idiom across all 47 chapters + the SOLID principles. ACCURATE to the 47 chapters (Wave D cross-checks every row). Word floor RELAXED (table-dominated). For-deeper-coverage: GoF + Martin PPP + /bibliography/.
- **F Programming principles reviewed in code** (`_docs/52`): ~1500-2000w prose. Principles each anchored to a real EMBODIMENT (named example dir/pattern, since the terms aren't used verbatim): DRY→Template Method/source-gen/analyzers (Pragmatic); Orthogonality→DI/Strategy/keyed services (Pragmatic); YAGNI→shortcut/anti-patterns (Pragmatic); KISS→delegate-vs-interface Strategy (Code Complete); composition-over-inheritance→Strategy/Decorator/Bridge (GoF); Law-of-Demeter/tell-don't-ask→Facade/Mediator (Pragmatic); fail-fast→guard clauses/ThrowIfNull/Tester-Doer/Dispose (Code Complete/Clean Code); Robustness/Postel→adapters/options-validation/parsing (Pragmatic); Boy-Scout/do-one-thing→legacy part seams/dependency-breaking/refactor-or-rewrite (Clean Code); separation-of-concerns→SRP/layering (Martin PPP); Brooks's Law (1 para)→Mythical Man-Month. Make explicit these are the vocabulary BEHIND the book's decisions; do NOT claim chapters name them. For-deeper-coverage: Pragmatic + Clean Code + Code Complete + Mythical Man-Month (+ GoF/Martin bridge) + /bibliography/. No new rows.
- **G C# 15 / .NET 11 delta** (`_docs/53`): additive, dated, SOURCE-CITED, preview-honest; per the research block. Length content-driven (~1200-1800, short OK, NO speculation/padding). Open by dating + RC/GA framing + "chapters stay on C#14, this is the one forward exception, every claim cites Learn." Body = per-affected-chapter delta (lead with runtime-native async [implementation NOT model change] + unions/closed hierarchies; then extension indexers, static interface members+numerics, async options, LINQ joins+collection-expr args; memory safety EXPLICITLY preview; labeled break/continue minor). Every feature carries a source URL; Wave C re-verifies each against live Learn at authoring. For-deeper-coverage: the two Learn pages + /bibliography/.

## Waves
- **Wave A (code): NONE. Wave B (diagrams): NONE** (count stays 112).
- **Wave C (prose, 2 agents): Agent 1 = E alone; Agent 2 = F + G.** Each: real LO+Why+body (keep 4 section names) + For-deeper-coverage w/ resolvable links; remove all TODO comments; self-gate voice (0 em-dash/ban-tier/rule-of-three/confessional/capstone/cross-language/`actually`); AI-sidebar 0; no When-not/Build-run-observe; `{% raw %}` around `{{`/`}}`. E/F net10.0/C#14; G is the ONLY C#15/.NET11 file, cites+dates+marks-preview, RE-VERIFIES every claim against live Learn URLs.
- **Wave D (validate, Opus):** all three — TODO gone; 4 sections; AI-sidebar 0; no When-not/BRO/excalidraw; front matter+names preserved; links resolvable; voice 0 em-dash/ban-tier/capstone/cross-language/actually; no bare "chapter NN"; jekyll clean; diagrams.yml 112; path-sanitized. E: every row accurate to the 47 chapters + SOLID. F: each principle anchored to a real embodiment + a LIVE bibliography row, no invented rows, ~1500-2000w. G (CRITICAL): every claim web-verifiable+cited+dated+preview-honest, RC/GA framing, feature→chapter map accurate, runtime-async=implementation-not-model, no speculative feature as settled, content-driven length. Write verification log.

## Acceptance criteria
Per appendix: TODO body + comments replaced; 4 sections only; no sidebar/when-not/BRO; AI-sidebar 0; links resolvable; front matter/names unchanged; voice clean; no example/diagram. E: table accurate to all 47 + SOLID, columns as specified, floor relaxed. F: ~1500-2000w, each principle anchored to real embodiment + live row, no new rows. G: every claim cited+dated+preview-honest, RC/GA framing, mapping accurate, content-driven length. Iteration: jekyll clean; diagrams.yml 112; no TODO in E/F/G; all three render; path-sanitized. **COMPLETES all book prose (47 chapters + Appendices A-G); only the 4 decks remain.**

## Risks
1. **G fabrication (HIGHEST):** .NET11/C#15 RC as of 2026-11-01 (GA 2026-11-10); docs may shift. §G-research gives verified anchors+URLs; Wave C re-verifies each claim; Wave D requires each web-verifiable+cited+dated; memory safety=preview; runtime-async=implementation-not-model; no proposed-as-settled.
2. **E accuracy to 47 chapters:** write from the confirmed 47-file list; Wave D cross-checks every row's "where it appears" against real chapters/examples.
3. **F principle accuracy:** vocabulary NOT named in chapters (only "single responsibility" in ch05); anchor to EMBODIMENT w/ named example dirs; Wave D = embodiment-accuracy + live-row attribution; no new rows.
4. **Standing gates:** 0 em-dash/rule-of-three/confessional/contrived-AI/capstone/cross-language; `{% raw %}` for `{{`/`}}`; jekyll clean; diagrams.yml 112; cross-ref relaxation (name patterns/dirs, no bare "chapter NN"); AI-sidebar 0.

## r24 verification log

<!-- Opus fills this in at Wave D. -->
