---
title: "r05 plan — Part 4 finish (ch9-10)"
output: false
render_with_liquid: false
---

# r05 — author ch9-10 (Part 4: the .NET substrate, finish)

Opus-planned 2026-10-29. Branch `release/r05` (based on main@16ecdbb). Both chapters
hands-on. Mirrors the r04 four-wave shape, scaled to two examples. Last Part 4 iteration.

## Scope

- **ch9** NRTs + records + init/required/with. Example: a `sealed record Consignment`
  (record CLASS) with `required` + `init` members and amendments via `with`.
- **ch10** Testing baseline with xUnit + NSubstitute. Example: a fresh
  `ShipmentRatingEngine` whose Tests project IS the teaching artifact (every xUnit /
  NSubstitute idiom the book relies on is a real green test the prose quotes).

## Confirmed facts

- Both stubs exist; edit body in place only, do NOT change front matter / `order` /
  `part` / include `name=`. `_docs/09-nrts-records-init-required-with.md` (order 9) has a
  `## When not to use it` section. `_docs/10-testing-with-xunit-and-nsubstitute.md`
  (order 10) has NO "When not to use it" section, so its single required AI sidebar lives
  in a body subsection (the closing "NSubstitute versus Moq" guidance).
- `_data/diagrams.yml` has 14 entries; new entries append in chapter order from #15.
- Amber accent (`#e8870c` / `#b8650a` / light fill `#fff8ef` / dark text `#111111`);
  README "purple" note is stale.
- Example layout template = `examples/08-value-types-struct-span/`. Root `global.json`
  MTP runner inherited; `scripts/verify.sh` auto-discovers `examples/*/*.Tests/*.csproj`.
- ch10 idiom references already in-tree: `examples/06-.../CarrierGateways.Tests/
  ShipmentRateAdvisorTests.cs`, `examples/05-.../RateCalculation.Tests/*`.

## Waves

- **Wave A (code, parallel):**
  - `examples/09-nrts-records-init-required-with/` (project `ConsignmentAmendments`):
    `sealed record Consignment` with `required string ConsignmentId`, `required Route`,
    `required Weight`, nullable `string? ReferenceNumber { get; init; }`; nested
    `sealed record Route(string Origin, string Destination)` + fresh `readonly record
    struct Weight`/`Money`. Amendments via `with`. Tests: value equality (recursive),
    `with` yields amended copy with original intact, required-member enforcement, NRT
    invariant (no null in a non-nullable member; nullable ReferenceNumber absent->present).
  - `examples/10-testing-with-xunit-and-nsubstitute/` (project `RatingEngine`): minimal
    lib (`ICarrierRates`/`ITaxPolicy`/`IAuditSink` + sealed `ShipmentRatingEngine` with
    async `QuoteAsync`, ConfigureAwait(false), token threaded); Tests project demonstrates
    [Fact]/[Theory]/[InlineData]/TheoryData, four-phase test, `Substitute.For<T>`,
    `.Returns`, `Received`/`DidNotReceive`, `Arg.Any`/`Arg.Is`, async substitutes. Fresh
    code (not ch5's), shipping-rating domain.
  - Each: library csproj with `<DefaultItemExcludes>...;<TestDir>/**`, nested `*.Tests/`
    (xunit.v3 4.0.2 / runner 4.0.1 / Test.Sdk 18.11.0 / NSubstitute 6.2.0; IsTestProject,
    IsPackable false, NoWarn CA1707; NO TestingPlatformDotnetTestSupport), a `.slnx`,
    a README. Capture real path-sanitized `dotnet test` output.
- **Wave B (diagrams):** one agent owns `_data/diagrams.yml`. #15
  `09-nrts-records-init-required-with` (record with required/init + a `with` amendment
  producing a new copy, original unchanged); #16 `10-testing-with-xunit-and-nsubstitute`
  (four-phase test around a system-under-test with three NSubstitute collaborators).
  Density figure per chapter only if that body passes ~2500 words.
- **Wave C (prose):** write each chapter after its example is green. ch9 body: record copy
  semantics; `required`+`init` replacing a validating ctor and silencing CS8618;
  non-destructive `with`; value equality + reference-type-member caveat; NRTs removing the
  Null Object reflex; AI sidebar in "When not to use it". ch10 body: four-phase test; the
  attributes + TheoryData; Substitute.For/Returns; Received/Arg matchers; async substitutes;
  closing "NSubstitute versus Moq" subsection carrying the ONE AI sidebar + the
  Moq/SponsorLink note. Prose quotes the real green tests. MS Learn citations (+ official
  xUnit/NSubstitute docs for ch10).
- **Wave D (validate):** idiomatic-review both examples; voice to 0 ban-tier; em-dash scan
  = 0 across both `.md` AND the examples' `.cs`/README (r04 lesson); `dotnet test` per
  example + `scripts/verify.sh`; `bundle exec jekyll build`; word counts; log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target, no ceiling; density figure if >~2500); full
spine (figure AFTER "Why this matters"); figure pair on disk + registered with matching
`name`; example `dotnet test` green + in `scripts/verify.sh`; tests assert behaviour;
"Build, run, observe" quotes real path-sanitized output (matching counts, 0 `/home/`,
0 username); exactly one `> **AI sidebar.**` (ch10's in the Moq subsection); zero em-dash;
no rule-of-three; idiomatic C#14 only, idiomatic-review clean; no intra-book numeric
cross-refs; framework claims cite MS Learn (+ xUnit/NSubstitute docs for ch10).
ch9 proves: recursive record value equality, `with` amended-copy with original intact,
required-member enforcement, NRT invariant. ch10 proves (as real green tests the prose
quotes): the attributes + TheoryData, four-phase, Substitute.For/Returns, Received/Arg,
async substitutes; AI sidebar + Moq/SponsorLink note present.
Iteration: `bundle exec jekyll build` clean; diagrams.yml appended from #15 in order;
new test csprojs omit TestingPlatformDotnetTestSupport; each example has its own `.slnx`.

## Risks

ch9: CS8618 (mark non-nullable members `required`+`init`); record struct vs class (use
`sealed record` class); shallow `with` + reference-type members (nested members are
records/value types; assert); required + primary-ctor interplay (non-positional record);
record equality with reference-type members. ch10: meta-drift (quote the real suite);
TheoryData/MemberData analyzer trips (xUnit1042/1019 - use `TheoryData<...>`); async
`.Returns`; xUnit1051 (TestContext.Current.CancellationToken); CA1707. Both: Liquid
`{{ }}`/`{% %}` in `with { }`/interpolation/attributes (wrap in raw; never wrap codetabs);
.slnx/global.json resolution; path leaks; word floor; diagram name drift.

## r05 verification log

Opus-validated 2026-10-29 (branch `release/r05`). Overall: PASS (after repair round).

**Build / test (all green):**
- `examples/09-nrts-records-init-required-with` `dotnet test`: 15 total / 15 succeeded / 0 failed.
- `examples/10-testing-with-xunit-and-nsubstitute` `dotnet test`: 16 total / 16 succeeded / 0 failed.
- `scripts/verify.sh`: both new examples auto-discovered and green alongside 00/05/06/07/08.
- `bundle exec jekyll build`: clean, all four r05 figures render.

**Content:** both chapters carry the full spine in order (figure after "Why this matters");
ch10 correctly has no "When not to use it" section (its one AI sidebar lives in the
"NSubstitute versus Moq" subsection). "Build, run, observe" quotes the real summaries with
matching counts (15 / 16), path-sanitized (0 `/home/`, 0 username); exactly one AI sidebar
each; body prose 4291 words (ch9) and 4166 words (ch10) by the validator's measure, both
above the 2000 floor and each carrying a second (density) figure; 0 em-dash in docs; no
intra-book numeric cross-refs; citations on learn.microsoft.com (+ official xunit.net /
nsubstitute.github.io / publisher link for ch10). The NSubstitute async-wrapping claim is
backed by the runnable `AsyncSubstituteTests` (NSubstitute's async doc page 404s), allowed
per the house rule.

**Diagrams:** four figures (`09-nrts-records-init-required-with`,
`09-required-init-construction-gap`, `10-testing-with-xunit-and-nsubstitute`,
`10-nsubstitute-stub-vs-verify`) each paired on disk, registered in `_data/diagrams.yml`
(entries 15-18, chapter order), included in the right chapter.

**Code idiom:** `lgtm-idiomatic-review` scan 0 hits on both examples. ch9 `Consignment` is a
`sealed record` (class) with `required`/`init` on every member, no CS8618 suppression. ch10
`ShipmentRatingEngine` uses ConfigureAwait(false) and threads the token; tests use
`TheoryData<,>` (xUnit1042/1019 clean) and `TestContext.Current.CancellationToken`
(xUnit1051 clean). Both test csprojs set IsTestProject + IsPackable false + NoWarn CA1707,
pin xunit.v3 4.0.2 / runner 4.0.1 / Test.Sdk 18.11.0 / NSubstitute 6.2.0, omit
`TestingPlatformDotnetTestSupport`; each example has its own `.slnx`. Em-dash scan across
both examples' `.cs` and README = 0 (r04 lesson held).

**Repairs applied this iteration (must-fix):**
- Replaced the two placeholder `Figure 9.1 / 10.1: TODO caption.` primary-figure captions
  with real descriptive captions (the density figures already had full captions).
- Rewrote 18+ ban-tier voice hits in `_docs/09` and `_docs/10` (genuinely/worth naming/on
  purpose/deliberately/full stop/honest way/"the rest of this book"); re-scan now 0
  ban-tier on both (watch-tier only). Load-bearing "genuinely" uses were reworded, not
  just deleted, to preserve meaning.
