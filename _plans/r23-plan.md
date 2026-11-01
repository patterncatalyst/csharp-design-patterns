---
title: "r23 plan — Appendices A-D"
output: false
render_with_liquid: false
---

# r23 — author Appendices A, B, C, D (build-and-test reference cluster)

Opus-planned 2026-11-01. Branch `release/r23` (based on main@11e0f40, r22 merge).
All 47 chapters (0-46) are authored + merged. Appendices split into 2 batches:
**r23 = A+B+C+D (build/test reference), r24 = E+F+G (synthesis + forward delta).**
Appendices are REFERENCE pages, lighter than chapters: end-sections = Learning
objectives + Why this matters + body + For deeper coverage ONLY. NO Build-run-observe,
NO When-not-to-use, NO AI-sidebar (gate `grep -c '<blockquote class="ai-sidebar"' == 0`
each), NO new example, NO diagram (D's optional figure declined — keep text-only).

## Confirmed facts
- Part slug `appendices` (all seven stubs + `_parts/15-*.md`). Stubs carry `appendix: "A".."G"` + `order:`. Keep front matter + the 4 section names; replace only `## TODO: body` + the LO/Why placeholders.
- diagrams.yml = 112 (UNCHANGED this batch — no figures).
- Meszaros row LIVE (Addison-Wesley 2007, informit link) — no new bibliography row needed. Principle sources for F (Batch 2) also live.
- **Appendix A accuracy traps (verified):** `Directory.Build.props` is at **`examples/`, NOT repo root** (net10.0, LangVersion 14.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, EnforceCodeStyleInBuild, AnalysisLevel latest-recommended, Deterministic). `global.json` (root) is ONLY `{"test":{"runner":"Microsoft.Testing.Platform"}}` — it selects the MTP runner, does NOT pin the SDK (floor enforced by `scripts/check-prereqs.sh`, major>=10). The four test pins (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0) live PER test csproj — there is NO Directory.Packages.props. `build.sh` = `dotnet build -c Release` over examples/*/*.csproj; `verify.sh` = `dotnet test --project <proj>` (MTP needs --project); `check-prereqs.sh` = dotnet present + major>=10. A must not claim an SDK pin that isn't there.

## Approach (per appendix)
Each: keep front matter verbatim; keep the 4 section names; write real Learning objectives + Why this matters; replace `## TODO: body` with real sections; write For-deeper-coverage with resolvable links; remove ALL TODO comments. AI-sidebar 0. Reference-appendix cross-ref relaxation: may name patterns/idioms and point at real example directories by name (e.g. `examples/10-testing-with-xunit-and-nsubstitute`); avoid bare "chapter NN" numeric pointers.
- **A Build and toolchain** (`47-...`): ground in the REAL files (props at examples/, global.json = MTP runner NOT SDK pin, per-csproj pins / no central packages file, the three scripts' real behavior, standalone vs whole-repo, editors VS Code+C# Dev Kit/Rider, no VS/no containers). No example, no diagram (the build-loop figure already exists at ch00; reference in prose). ~1200-1800 words.
- **B xUnit reference (v3)** (`48-...`): [Fact]/[Theory]/[InlineData]/[MemberData]/TheoryData, Assert.*, TestContext.Current.CancellationToken, the MTP runner (`dotnet test --project`), `Method_does_thing` naming + test-only NoWarn CA1707. Accurate to v3 (xunit.v3 4.0.2 / TestContext), NOT v2. Illustrate against real suites. No example/diagram. ~1200-1800.
- **C NSubstitute reference (6.2.0)** (`49-...`): Substitute.For<T>, Returns/ReturnsForAnyArgs, Received()/Received(n)/DidNotReceive(), Arg.Any/Arg.Is, throwing + Task-returning substitutes, the Moq note (never migrate a working Moq suite). No example/diagram. ~1000-1500.
- **D xUnit test patterns (Meszaros)** (`50-...`): four-phase test (Arrange/Act/Assert + teardown) + test-double taxonomy (dummy/stub/spy/mock/fake), mapping NSubstitute onto it (substitute-as-stub via Returns vs substitute-as-mock via Received). Cite Meszaros via /bibliography/ + informit link. No example; NO diagram (declined — reads fine as a list). ~1200-1800.
For-deeper-coverage for all four: official xUnit v3 docs, NSubstitute docs, Microsoft Learn, Meszaros publisher link, + the existing /bibliography/ trailing line.

## Waves
- **Wave A (code): NONE.**
- **Wave B (diagrams): NONE** (no figures this batch).
- **Wave C (prose, 2 parallel agents):** Agent 1 = A+B; Agent 2 = C+D. Each writes real LO + Why + body (keep section names) + For-deeper-coverage, resolvable links, removes all TODO comments, self-gates voice (0 ban-tier, 0 em-dash, 0 capstone, 0 cross-language, 0 "actually"), AI-sidebar 0, net10.0/C#14 scope (no C# 15 — that's G).
- **Wave D (validate, Opus):** per appendix — TODO body + TODO comments gone; 4 sections exactly; AI-sidebar grep 0; no When-not-to-use / Build-run-observe; links resolvable + accurate; voice 0 ban-tier/em-dash/capstone/cross-language; word guidance met; front matter + section names preserved; jekyll clean; diagrams.yml still 112. A: file/path/script claims match the repo (props at examples/, global.json=MTP-not-SDK-pin, per-csproj pins, script behavior). B: v3-only, no v2 API. C: 6.2.0 idioms + Moq-no-migrate. D: four-phase + five-double taxonomy correct, Meszaros link resolves. Write verification log.

## Acceptance criteria
Per appendix: TODO body replaced; 4 sections only (LO/Why/body/For-deeper-coverage); no sidebar/when-not/BRO; AI-sidebar 0; links resolvable; front matter/section names unchanged; word guidance met; net10.0/C#14 scope; voice clean (0 em-dash/ban-tier/capstone/cross-language). A: every file/path/script claim matches repo. B: v3-only. C: 6.2.0 + non-migratory Moq note. D: taxonomy correct + Meszaros resolves. Iteration: jekyll clean; diagrams.yml 112 (unchanged); no TODO strings in A-D; all four render in appendices nav.

## Risks
- **Appendix A file accuracy (primary this batch):** props-at-examples/ and global.json-is-not-an-SDK-pin are the likely errors; Wave D reads the real files.
- **B v2-vs-v3 drift:** pin to the xunit.v3 surface + TestContext.Current.CancellationToken; no v2 idioms.
- **Standing voice/jekyll:** 0 em-dash, no rule-of-three/confessional/contrived-AI, no capstone, no cross-language; `{% raw %}` around `{{`/`}}` in fenced snippets; jekyll passes. Cross-ref relaxation intentional (name examples/patterns, no bare "chapter NN").

## Batch 2 sketch (r24, plan later): E pattern-to-force table (map all 47 chapters, accuracy risk), F programming principles (DRY/YAGNI/KISS/composition/Demeter/fail-fast + SOLID bridge, cite live rows), G C# 15/.NET 11 delta (HIGHEST fabrication risk — web-verify + date + cite every claim, additive, honest about preview status).

## r23 verification log

<!-- Opus fills this in at Wave D. -->
