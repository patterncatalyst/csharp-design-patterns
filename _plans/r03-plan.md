---
title: "r03 plan — Part 3 (ch5-6)"
output: false
render_with_liquid: false
---

# r03 — author ch5-6 (Part 3: Principles)

Opus-planned 2026-10-29. Branch `release/r03`. Both chapters hands-on. Mirrors the
r02 shape, scaled to two examples.

## Waves

- **Wave A (code):** build `examples/05-srp-ocp-lsp/` and
  `examples/06-isp-dip-and-built-in-di/` (independent, parallel) to green under a
  bare `dotnet test` (each has its own `.slnx`; the repo-root `global.json` MTP
  runner is inherited). Capture real output per chapter.
- **Wave C (prose):** write each chapter after its example is green (its
  "Build, run, observe" quotes real output). ch5 and ch6 independent.
- **Wave B (diagrams):** one agent owns `_data/diagrams.yml`; draws one figure per
  chapter (plus a density figure if a body passes ~2500 words), appended after the
  existing 7 entries.
- **Wave D (validate):** idiomatic-review on both examples, voice + em-dash scan on
  both chapters, `scripts/verify.sh`, `bundle exec jekyll build`, word counts, log
  to `_plans/`.

## Example layout (established in r02, reuse exactly)

`examples/NN-<slug>/`: library `.csproj` (with `<DefaultItemExcludes>...;*.Tests/**`),
domain `.cs`, nested `*.Tests/` test project (xunit.v3 4.0.2 / runner 4.0.1 /
Test.Sdk 18.11.0 / NSubstitute 6.2.0; `IsTestProject`, `IsPackable false`,
`NoWarn CA1707`; NO `TestingPlatformDotnetTestSupport` property, it is a no-op),
a `.slnx` referencing both, a README. Inherits `examples/Directory.Build.props`.
ch6 library adds a pinned `Microsoft.Extensions.DependencyInjection` reference.

## Chapter intent

- **ch5 SRP, OCP, LSP** (Martin). Example: a `RateCalculator` split into
  single-responsibility collaborators (pricing / tax / audit). OCP test: a new
  surcharge type needs no edit to pricing. Meaningful LSP test: two implementations
  honour the same invariants (non-negative, monotonic in weight, input unmutated).
  Idiomatic modern C# 14 only; no deliberately-bad "before" class as code.
- **ch6 ISP, DIP, built-in DI** (Martin + docs). Example: `ICarrierGateway` behind
  `Microsoft.Extensions.DependencyInjection`; resolve from `IServiceProvider`, swap
  by changing only the registration, ISP split interface. DI claims cite Microsoft
  Learn; Java/Spring contrast in prose only.

## Acceptance criteria

Per chapter: body >= 2000 words (target 2500, no ceiling; density figure if >~2500);
full spine (figure AFTER "Why this matters"); figure pair on disk + registered with
matching `name`; example `dotnet test` green + picked up by `scripts/verify.sh`,
tests assert behaviour; zero em-dash, no rule-of-three; idiomatic C# 14 only,
idiomatic-review clean; no intra-book numeric cross-refs. ch6 DI claims cite MS Learn.
Iteration: `bundle exec jekyll build` clean; diagrams.yml entries appended in order;
new test csprojs omit `TestingPlatformDotnetTestSupport`.

## Risks

Liquid `{{ }}` in DI/collection-expression snippets (wrap in raw; caught by jekyll
build). New-example `.slnx`/global.json resolution (caught by first bare
`dotnet test`). Trivial LSP test (must parametrize a behavioural invariant over two
impls). xUnit1051 / CA1707 under warnings-as-errors. Uncited DI claims. Word under
floor. Diagram name drift. `Microsoft.Extensions.DependencyInjection` pin.

## r03 verification log

_(filled in Wave D)_
