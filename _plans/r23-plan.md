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

Validated 2026-11-01 (Opus, Wave D), adversarial read of the real files on branch
`release/r23`. Verdict: **PASS**.

### Structure gates (all four files) — MET
- Front matter preserved on all four (title, order, part: appendices, appendix:, description).
- Exactly one each of `## Learning objectives`, `## Why this matters`, `## For deeper coverage`; real body sections (no `## TODO: body`).
- `## Build, run, observe` 0, `## When not to use it` 0, `<blockquote class="ai-sidebar"` 0, `TODO` 0, `excalidraw.html` 0 — every file.

### Accuracy — Appendix A — MET (verified against repo)
- `Directory.Build.props` lives at `examples/`, NOT repo root (`find` confirms one file only). All seven properties match the real file: net10.0, LangVersion 14.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, EnforceCodeStyleInBuild, AnalysisLevel latest-recommended, Deterministic.
- `global.json` = `{"test":{"runner":"Microsoft.Testing.Platform"}}` only; appendix correctly states it does NOT pin the SDK.
- No `Directory.Packages.props` exists (`find` = none); four pins (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0) live per test csproj — confirmed in `examples/10-.../RatingEngine.Tests/RatingEngine.Tests.csproj`.
- Script behavior matches: build.sh = `dotnet build -c Release` over `examples/*/*.csproj`; verify.sh = `dotnet test --project`; check-prereqs.sh = dotnet present + major>=10. The `check_dotnet()` snippet in the appendix is a verbatim match.
- `TestingPlatformDotnetTestSupport` is NOT set anywhere in examples/ and the appendix does not claim it is.

### Accuracy — Appendix B — MET
- v3-only: 0 v2-era signals (Xunit.Abstractions / ITestOutputHelper / IClassFixture / [Collection] / ICollectionFixture); xunit.v3 named 4x. Covers Fact/Theory/InlineData/MemberData/TheoryData, Assert.*, TestContext.Current.CancellationToken, xUnit1051, MTP `dotnet test --project`, CA1707 note. Pins table matches the csproj; the CA1707 NoWarn block quoted matches the real csproj exactly.

### Accuracy — Appendix C — MET
- NSubstitute 6.2.0 idioms correct (Substitute.For, Returns/ReturnsForAnyArgs, Received/Received(n)/DidNotReceive/DidNotReceiveWithAnyArgs/Received.InOrder, Arg.Any/Arg.Is, throwing + async). Moq note non-migratory: "never tells a team to migrate a working Moq suite."

### Accuracy — Appendix D — MET
- Four-phase test + five-double taxonomy (dummy/stub/spy/mock/fake) correct per Meszaros; NSubstitute→taxonomy mapping sound. Cited source files all exist and quoted snippets match byte-for-byte: FakeClock.cs, InMemoryHandlingLedger.cs (Entries/AppendAsync/Interlocked), FakeCarrierGateway.cs doc comment, ReceivedTests.cs BuildEngine, DependencyBreakingTests.cs (`BuildEngine(out var tariffs, out _, ...)` and the audit.Received(1).Record predicate). Meszaros row + informit link present in bibliography.html (line 61-62).

### Voice + site gates — MET
- lgtm-professional-voice scan: 0 ban-tier each (C and D = 0 matches total; A = 4 watch-tier, B = 3 watch-tier, all benign reference-doc patterns: filename-as-heading on the `Directory.Build.props:` / `global.json:` section titles, "quietly", hedging/"spec-violation framing").
- em-dash 0, capstone 0, `\bactually\b` 0, cross-language (java/c++/jvm/spring/rust/golang) 0, bare `chapter N`/`chN` numeric pointers 0 — every file.
- `bundle exec jekyll build` clean, no Liquid errors. diagrams.yml = 112 (unchanged). All four appendices render under `_site/docs/`. Path-sanitized: `/home/` 0, username 0 each.

### Word counts (prose, excl. code + headings)
- A 1723 (target 1200-1800) ✓ · B 1482 (1200-1800) ✓ · C 1329 (1000-1500) ✓ · D 1537 (1200-1800) ✓

### Observation (out of scope, NOT an appendix defect)
- `scripts/check-prereqs.sh` header comment (line 10-11) still references `Directory.Packages.props`, which does not exist in the repo. Appendix A does NOT repeat this error (it correctly states pins are per-csproj). Pre-existing stale script comment; flag for a future cleanup, not an r23 blocker.
