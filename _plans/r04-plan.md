---
title: "r04 plan — Part 4 (ch7-8)"
output: false
render_with_liquid: false
---

# r04 — author ch7-8 (Part 4: the .NET substrate)

Opus-planned 2026-10-29. Branch `release/r04` (based on main@4922e54). Both chapters
hands-on. Mirrors the r03 four-wave shape, scaled to two examples. ch9-10 are a later
iteration (out of scope here).

## Scope

- **ch7** Deterministic cleanup — `IDisposable` / `IAsyncDisposable`, `using` /
  `await using`, the dispose pattern. Example: `ManifestWriter` owning a file handle
  (sync `Dispose`) + an async ledger-append path (`IAsyncDisposable` / `await using`).
- **ch8** Value types — `struct`, `readonly struct`, `ref struct`, `Span<T>` /
  `ReadOnlySpan<T>`. Example: `Money` / `Weight` / `SlotId` as `readonly record struct`
  + a Span-based fixed-width EDI parse with no intermediate string allocations.

## Confirmed facts (reused from exploration)

- Both reader files exist as stubs with correct front matter + figure includes:
  `_docs/07-dispose-and-iasyncdisposable.md` (part `dotnet-substrate`, order 7,
  include name `07-dispose-and-iasyncdisposable`) and
  `_docs/08-value-types-struct-span.md` (order 8, include name
  `08-value-types-struct-span`). Edit in place; do NOT change front matter, `order`,
  `part`, or the include `name=`.
- `_data/diagrams.yml` has exactly 10 entries; new entries append in chapter order.
- Amber accent (`#e8870c` / `#b8650a` / light fill `#fff8ef` / dark text `#111111`);
  the "purple" line in `diagrams/README.md` is stale.
- Example layout copies `examples/06-isp-dip-and-built-in-di/` exactly: library csproj
  with `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**`, nested `*.Tests/`
  (xunit.v3 4.0.2 / xunit.runner.visualstudio 4.0.1 / Microsoft.NET.Test.Sdk 18.11.0 /
  NSubstitute 6.2.0; `IsTestProject`, `IsPackable false`, `NoWarn CA1707`; NO
  `TestingPlatformDotnetTestSupport`), a `.slnx`, a README. Inherits
  `examples/Directory.Build.props` + root `global.json` MTP runner.
- `scripts/verify.sh` auto-discovers `examples/*/*.Tests/*.csproj`; no edit needed.
- Spine order: Learning objectives -> Why this matters -> FIGURE -> body -> Build, run,
  observe -> "when not to use" section with exactly one `> **AI sidebar.**` -> For
  deeper coverage. "Build, run, observe" quotes only the real path-sanitized
  `Test run summary` block.

## Waves

- **Wave A (code):** build `examples/07-dispose-and-iasyncdisposable/` and
  `examples/08-value-types-struct-span/` (independent, parallel) to green under a bare
  `dotnet test` (each has its own `.slnx`; root `global.json` MTP runner inherited).
  Capture real path-sanitized output per chapter.
  - ch7: sealed `ManifestWriter : IDisposable` owning a `FileStream` (`_disposed` guard,
    `ObjectDisposedException.ThrowIf`, no finalizer since sealed + managed-only); sealed
    `LedgerAppender : IAsyncDisposable` (async append, `DisposeAsync` flush+close,
    `ConfigureAwait(false)`). Tests assert handle released, use-after-dispose throws,
    ledger line on disk after disposal, deterministic scope-exit cleanup. Temp files
    under `Path.GetTempPath()`, deleted in `finally`; token on every async call.
  - ch8: `Money` / `Weight` / `SlotId` as `readonly record struct` (value equality,
    CA1815 satisfied, validating factory where meaningful); `ref struct` /
    `ReadOnlySpan<char>` fixed-width EDI parser (`TryParse`, `Slice`, culture-invariant
    numeric parse, no string allocs). Tests assert value semantics, parse correctness,
    malformed rejection. No-alloc shown in prose+code (GC assertion only if robust).
    Keep span work out of async/iterator/lambda.
- **Wave B (diagrams):** one agent owns `_data/diagrams.yml`. ch7 `using` / `await using`
  lifecycle figure; ch8 stack-struct + span-window figure. Amber accent, appended after
  the existing 10 entries, `name:` matching SVG basename + include. One density figure
  per chapter only if that body passes ~2500 words.
- **Wave C (prose):** write each chapter after its example is green ("Build, run,
  observe" quotes real output). ch7 and ch8 independent. Full spine, one AI sidebar,
  MS Learn citations, zero em-dash, idiomatic C# 14 only, no intra-book numeric refs.
- **Wave D (validate):** idiomatic-review on both examples, voice + em-dash scan on both
  chapters, `dotnet test` per example, `scripts/verify.sh`, `bundle exec jekyll build`,
  word counts; log to the verification section below.

## Acceptance criteria

Per chapter: body >= 2000 words (target 2500, no ceiling; density figure if >~2500);
full spine (figure AFTER "Why this matters"); figure pair on disk + registered with
matching `name`; example `dotnet test` green + picked up by `scripts/verify.sh`, tests
assert behaviour; "Build, run, observe" quotes real path-sanitized output; exactly one
`> **AI sidebar.**`; zero em-dash, no rule-of-three; idiomatic C# 14 only,
idiomatic-review clean; no intra-book numeric cross-refs; framework claims cite MS Learn.
ch7 cites IDisposable/IAsyncDisposable/dispose-pattern/`using`; ch8 cites
struct/readonly-struct/ref-struct/`Span<T>`/implicit-span-conversions.
Iteration: `bundle exec jekyll build` clean; diagrams.yml entries appended in order;
new test csprojs omit `TestingPlatformDotnetTestSupport`; each example has its own `.slnx`.

## Risks

CA2007 (ConfigureAwait in library `DisposeAsync`/async). xUnit1051 (token on async
calls). ref struct / `Span<T>` analyzer rules (no span across await/iterator/lambda).
struct `default` / CA1815 equality (use `readonly record struct`; design `default` to
stay valid). CA1304/CA1305 (culture on span parse). GC-alloc assertion flakiness
(fall back to prose+code). Liquid `{{ }}` in snippets (wrap in raw; caught by jekyll
build). `.slnx` / global.json test resolution. CA1707 leakage. CA1063/CA1816 dispose
pattern. Path leak in quoted output. Word under floor. Diagram name drift.

## r04 verification log

_(filled in Wave D)_
