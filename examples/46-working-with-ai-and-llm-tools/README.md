# 46-working-with-ai-and-llm-tools

A Roslyn analyzer that flags a specific shape of pattern-shaped LLM output: a
Strategy interface with exactly one implementation in the compilation.

- **The smell.** An LLM asked to price something will often reach for a
  Strategy interface before a second strategy exists to justify it. The
  interface compiles, the single implementation compiles, and nothing in the
  build points out that the abstraction has no second case to serve. By
  convention this analyzer treats any interface whose name ends in
  `Strategy` as a candidate, and walks every class and struct in the
  compilation to count how many implement it.
- **The rule.** `SingleImplementationStrategyAnalyzer` (diagnostic id
  `SHIP0002`) reports a warning on a candidate Strategy interface that has
  exactly one implementing type in the compilation, naming both the
  interface and its sole implementation so the warning is actionable on
  sight. An interface not named `*Strategy`, a `*Strategy` interface with
  zero implementations, and a `*Strategy` interface with two or more
  implementations are all left alone.
- **The corrected sample.** `RatingReview.csproj` hosts `IRatingStrategy`
  with two genuine implementations, `StandardRatingStrategy` and
  `ExpressRatingStrategy`, each pricing a `BookingRequest` with its own
  formula. This is the reviewed, two-strategy form of a sample an LLM first
  drafted with only one strategy in place, so the analyzer has nothing to
  flag and the solution builds with zero `SHIP0002` diagnostics.
- **Project shape.** `RatingReview.csproj` is the consuming library at the
  top level. `RatingReview.Analysis/` is a nested `netstandard2.0` project,
  the floor every Roslyn-hosting compiler can load an analyzer assembly
  from, referenced back into the consuming project with
  `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`: it
  participates in compilation as a compiler plugin, never as an assembly
  reference. `RatingReview.Tests/` is a normal nested xUnit v3 project.

**Pinning the compiler.** The .NET 10 SDK 10.0.113 hosts Roslyn 5.0.0.
`RatingReview.Analysis.csproj` pins `Microsoft.CodeAnalysis.CSharp` to that
same `5.0.0` (not NuGet-latest, which at the time of writing is newer than
the host and trips `CS9057` under warnings-as-errors) plus
`Microsoft.CodeAnalysis.Analyzers` `4.14.0`, both `PrivateAssets="all"` so
neither becomes a runtime dependency of the shipped analyzer.
`EnforceExtendedAnalyzerRules` is on, which in turn requires release
tracking for every diagnostic id (`RS2008`); `AnalyzerReleases.Shipped.md`
and `AnalyzerReleases.Unshipped.md` declare `SHIP0002` as unshipped rather
than suppressing the rule.

**What the tests prove.** `RatingReview.Tests` runs the analyzer through the
raw Roslyn compiler API, not a `*.Analyzer.Testing.Xunit` package:
`AnalyzerTestHarness` compiles a source string into an in-memory
`CSharpCompilation` using the host's trusted-platform-assembly list for
references, then calls `WithAnalyzers` and `GetAnalyzerDiagnosticsAsync`.
Every test snippet is a complete, self-contained compilation unit.
`SingleImplementationStrategyAnalyzerTests` confirms the analyzer fires on a
`*Strategy` interface with one implementation, stays quiet when that
interface has two implementations, stays quiet on a single-implementation
interface not named `*Strategy` (the naming-scope guard), stays quiet on a
`*Strategy` interface with no implementations at all, reports `SHIP0002` at
`Warning` severity located on the interface's own identifier, names both the
interface and its sole implementation in the message, and flags two
unrelated single-implementation `*Strategy` interfaces independently in the
same compilation.

## Build and test

```sh
cd examples/46-working-with-ai-and-llm-tools
dotnet build RatingReview.slnx
dotnet test --solution RatingReview.slnx
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
