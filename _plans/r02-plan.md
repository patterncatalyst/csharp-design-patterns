---
title: "r02 plan — chapters 0-4"
output: false
render_with_liquid: false
---

# r02 — author chapters 0-4 (Parts 0-2)

Opus-planned 2026-10-29. Branch `release/r02`. Scope: ch0 (prerequisites, hands-on)
+ ch1-ch2 (Part 1, not-a-GoF-retelling) + ch3-ch4 (Part 2, not-the-Java-book),
their five paired diagrams, the single hands-on example
(`examples/00-prerequisites/`), the shared `examples/Directory.Build.props`, the
`_data/diagrams.yml` registrations, and a clean Jekyll build.

## Approach — four waves

- **Wave A (code):** `examples/Directory.Build.props` then `examples/00-prerequisites/`
  smoke test (Consignment record + xUnit v3 behaviour tests), green under
  `dotnet test`. Capture real output for ch0.
- **Wave B (diagrams):** five paired SVG + `.excalidraw` figures + register in
  `_data/diagrams.yml`. Concurrent with A.
- **Wave C (prose):** ch1-ch4 bodies (independent); ch0 body after Wave A so its
  "Build, run, observe" output is real.
- **Wave D (validate):** voice pass, em-dash/rule-of-three scan, `dotnet test` +
  `verify.sh`, `bundle exec jekyll build`, notes to `_plans/` only.

## Steps / status

| # | Step | Files | Status |
|---|------|-------|--------|
| 1 | Shared build props | `examples/Directory.Build.props` | done |
| 2 | ch0 smoke-test example | `examples/00-prerequisites/**` | done (6 tests green) |
| 3 | Generate diagrams (7: 5 primary + ch1/ch3 density figures) | `diagrams/*.svg/.excalidraw` | done |
| 4 | Register diagrams | `_data/diagrams.yml` (7 entries) | done |
| 5 | ch1-ch4 prose | `_docs/0{1..4}-*.md` | done |
| 6 | ch0 prose | `_docs/00-prerequisites.md` | done |
| 7 | Iteration validation | `_docs/*`, `_plans/` | done |

## Acceptance criteria

All five chapters: body prose >= 2000 words (target 2000-2500); full spine
(Learning objectives, Why this matters before mechanics, required figure, body,
For deeper coverage citing `/bibliography/`); paired figure present + registered
with matching `name`; zero em-dash, no rule-of-three; no baked chapter/part
numbers or internal codes inside the SVG drawing; light-fill/dark-text emphasis
boxes; no intra-book "ch N" number cross-refs in prose.

ch0: states Fedora/Linux-only + no-containers up front; pins dated .NET 10 SDK;
VS Code + C# Dev Kit or Rider and `dotnet` CLI only; "Build, run, observe" quotes
real `dotnet test` output; `examples/Directory.Build.props` has nullable-on +
warnings-as-errors; example tests assert Consignment behaviour (value equality,
`with` amendment, constructor validation), standalone-testable and picked up by
`scripts/verify.sh`.

ch2, ch3 (original research): every language-feature claim cites Microsoft Learn
or the C# spec, or a runnable snippet; no blog citations. ch3 codetab (if used)
is the bare `{% include codetabs.html langs="Java|C#" %}`, one fenced block per
label, not wrapped in raw tags.

Iteration-wide: `bundle exec jekyll build` succeeds, all figures render, no leaked
Liquid; `_data/diagrams.yml` has seven entries in chapter order (five primary plus
the ch1 and ch3 density figures); verification notes only in `_plans/`.

## Toolchain pins (verified 2026-10-29, SDK 10.0.113)

`xunit.v3` 4.0.2, `xunit.runner.visualstudio` 4.0.1, `Microsoft.NET.Test.Sdk`
18.11.0; NSubstitute surveyed + pinned at creation. Test csproj needs
`<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`. Pass
`TestContext.Current.CancellationToken` on token-taking calls (xUnit1051).

## Risks

- 🔬 ch2/ch3 feature claims without runnable basis -> require citation or compiled
  snippet per claim.
- Liquid `{{ }}` in C# snippets breaks the build -> wrap in `{% raw %}`; caught by
  `bundle exec jekyll build`, not plain `dotnet build`.
- xUnit1051 / VSTest-removed -> caught by first `dotnet test`; fixes above.
- Missing/mis-scoped Directory.Build.props -> confirm a deliberate nullable
  warning fails the build.
- Word count under the 2000 floor on the conceptual chapters -> count each body.
- Diagram name drift across SVG / include / diagrams.yml -> cross-check base names.

## r02 verification log

Opus-validated 2026-10-29, all 10 criteria MET. Evidence:

- `bundle exec jekyll build` clean; all 7 r02 figures render as real SVGs.
- `examples/00-prerequisites`: `dotnet test` 6/6 green; `scripts/verify.sh` green;
  tests assert behaviour (value equality, `with`-amendment, constructor validation).
- Warnings-as-errors + nullable enforced from `examples/Directory.Build.props`.
- Body word counts (house voice_stats metric): ch0 2441, ch1 2168, ch2 2234,
  ch3 2099, ch4 2070 (after a closing-paragraph addition to clear the floor). All
  >= 2000.
- Voice: 0 ban-tier, 0 em-dash across all five chapters. Code: lgtm-idiomatic-review
  scan 0 findings.
- ch2/ch3 feature claims cite Microsoft Learn / the C# spec; zero blog hosts.
- No `/home/` or username leak; no sibling-site cross-refs; no intra-book numeric
  chapter cross-references.

Toolchain findings to fold back into the `lgtm-csharp` skill (follow-up, separate
PR): on this .NET 10 SDK the real `dotnet test` opt-in is a root `global.json`
`{"test":{"runner":"Microsoft.Testing.Platform"}}` (the
`TestingPlatformDotnetTestSupport` property is a no-op under MTP v2); each
nested-`Tests` example needs a `.slnx` so a bare `dotnet test` resolves the test
project; `dotnet test` now wants `--project` not a bare positional path; CA1707
(underscore test names) is suppressed in the test csproj only.
