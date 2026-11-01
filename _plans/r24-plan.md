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

### Wave D adversarial validation — Opus, 2026-11-01

Verdict: **PASS-WITH-NITS**. Two substantive content fixes routed to Sonnet (one in
F, one in G); everything else MET. All book prose (47 chapters + Appendices A-G) is
complete; only the four companion decks remain.

**Structure gates (all three): MET.** Front matter preserved (title/order/part:
appendices/appendix:/description). Each file has exactly one `## Learning objectives`,
`## Why this matters`, a real body, and one `## For deeper coverage`. grep -c == 0
each for: `<blockquote class="ai-sidebar"`, `excalidraw.html`, `When not to use`,
`Build, run, observe`, `TODO`. `grep -rl '## TODO: body' _docs/` is empty.

**E (accuracy to 47 chapters): MET.** 11 rendered sub-tables, 71 rows, columns
exactly `Pattern / principle / idiom | Where it appears | Forces it answers |
Modern C# note`. Cross-checked all ~47 "Where it appears" cells against actual
chapter titles (ch00-46) — every one maps to a real chapter/area; no invented
patterns, no misplacement. All five SOLID principles present as rows
(SRP/OCP/LSP in the SRP/OCP/LSP row group, ISP/DIP in ISP/DIP/DI). No bare
"chapter NN"/`chN` pointers (grep == 0). net10.0/C#14 scope, no C#15 content.
Renders (table present in _site).

**F (principle-to-embodiment + live citations): MET except one Facade paragraph.**
Verified embodiments in real example dirs: DRY→Template Method (ex12) + source-gen/
analyzers; YAGNI→`DocumentDispatcher`+`FrozenDictionary` (ex39) and anti-patterns
`IRateComponent`/`IRateComponentFactory` (ex40); KISS→Strategy delegate vs interface
(ex11); composition→Strategy/Decorator/Bridge; fail-fast→`ArgumentNullException.ThrowIfNull`
(confirmed in ex12/13/23/29) + Dispose; Robustness→adapters + `CarrierGatewayOptionsValidator`/
`ValidateOnStart` (ex30) + Tester-Doer parsing; Boy-Scout→`SeamedRateEngine`/`IRateEngine`
(ex43/44); SoC→SRP/layering; Brooks→refactor-or-rewrite. F correctly does NOT claim the
chapters name the principles (notes only "single responsibility" in ch05). All six
citations map to LIVE bibliography rows (Pragmatic Programmer, Clean Code, Code Complete,
Mythical Man-Month, GoF, Martin PPP) — no invented rows. Body prose ~2255 words
(~2000 excluding For-deeper-coverage), slightly above the ~1500-2000 estimate but within
the book's no-ceiling policy, not padding.
  - **NOT MET (substantive, route to Sonnet):** the Law-of-Demeter/Facade paragraph
    mis-describes the real example. It names a `ShippingFacade` handling a "label-printing
    request" that chains "a carrier gateway, a document renderer, and a manifest writer."
    The actual class is `ShipmentBookingFacade` (ex18), whose `Book` call composes three
    DIFFERENT subsystems: `IAddressValidator` (address validation), `ICarrierGateway`
    (carrier quoting), and `ICustomsFiler` (customs filing). `ManifestWriter` belongs to
    ch07 (Dispose); `DocumentRenderers` to ch39 (shortcut patterns). Fix: rewrite the
    sentence to the real `ShipmentBookingFacade` — one `Book` call over address validation,
    carrier quoting, and customs filing. (Not a trivial name swap; the operation and all
    three collaborators are wrong, so left for a domain-accurate repair.)

**G (fabrication control — CRITICAL): MET except one inverted memory-safety sentence.**
Verified every feature claim against the live Microsoft Learn pages fetched 2026-11-01:
  - Release status honest/dated: "last updated for release candidate 2 (RC 2)" and GA
    "expected in November 2026" are exact quotes from the .NET 11 overview. GA Nov 10 2026
    and the STS **two-year** support length are confirmed on the dotnet support-policy page.
    The Nov 10-12 date is attributed to the .NET Conf 2026 announcement (not asserted as a
    Learn fact), consistent with GA Nov 10 — acceptable per gate. (Minor watch: the phrase
    "the length Microsoft set for STS releases starting with .NET 9" — the 2-yr STS length
    is confirmed, the "starting with .NET 9" attribution is not independently verified here;
    not a blocker.)
  - Memory safety explicitly labeled a C# 15 PREVIEW (LangVersion preview +
    `updated-memory-safety-rules` flag) — MET.
  - Runtime async framed as an implementation change, not a change to the stackless model —
    MET. "cleaner stack traces and lower overhead", the net11.0 no-`EnablePreviewFeatures`
    note, `runtime-async=on`, and the three JIT follow-ons all match the overview verbatim.
  - Every feature verified on Learn: union types, closed hierarchies, extension indexers,
    non-virtual static interface members on older runtimes, Decimal32/64/128, generic
    `Complex<T>`, `Random.NextInteger<T>`/`NextBinaryFloat<T>`, async options
    (`IAsyncStartupValidator`/`AsyncValidationAttribute`/`IAsyncValidatableObject`), LINQ
    `FullJoin` + tuple `Join`/`GroupJoin` across Enumerable/Queryable/AsyncEnumerable,
    collection expression arguments `with(...)`, labeled break/continue. Every section
    carries a Learn URL. No proposed-but-unaccepted feature stated as settled. Additive;
    chapters stay on C#14; G is the only C#15 file. Body ~1722 words (within ~1200-1834).
  - **NOT MET (substantive, route to Sonnet):** the memory-safety section inverts the
    `unsafe` operation list. G states `unsafe` now marks "the specific operations that touch
    unmanaged memory directly, address-of, `fixed`, a `stackalloc`-to-pointer conversion,
    and `sizeof`." Per the Learn C# 15 page those four are exactly the operations that NO
    LONGER require an `unsafe` context under the preview; the operations that STILL require
    `unsafe` (the ones that access pointed-to memory) are pointer dereference, pointer
    member access (`p->member`), pointer element access (`p[i]`), and function-pointer
    invocation. Fix: swap the grouping so the relaxed set (address-of/`fixed`/`stackalloc`
    -to-pointer/`sizeof`) is named as no-longer-requiring-unsafe and dereference/access is
    named as still-requiring it.

**Voice + site gates (all three): MET.** `scan.sh` 0 ban-tier each (watch-tier only:
E=2, F=4, G=0). `grep -c '—'`==0, `grep -ci capstone`==0, `grep -ci '\bactually\b'`==0,
cross-language grep==0 each. `bundle exec jekyll build` clean, no Liquid errors.
`_data/diagrams.yml` == 112. All three render in `_site`; rendered pages path-sanitized
(`/home/` and username == 0 each).

**Word counts.** E: ~550 framing words + table (~2496 table words). F: ~2255 body.
G: ~1722 body.

**Completion.** No `## TODO: body` remains in any `_docs` file. All 47 chapters (00-46)
plus Appendices A-G are authored. Book prose is COMPLETE; only the four companion decks
(101/201/301/401) remain.
