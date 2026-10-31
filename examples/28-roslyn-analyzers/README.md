# 28-roslyn-analyzers

A Roslyn analyzer that enforces a shipping-domain wire-contract rule at
compile time: a type that crosses a process boundary must never model money
as `decimal`.

- **The rule.** `[WireContract]` marks a type as something that is
  serialized across an HTTP, gRPC, or message-queue boundary. `decimal` has
  no fixed cross-platform wire representation, so money on a wire-contract
  type should travel as an integer count of minor units (cents) instead.
  `MoneyAsDecimalAnalyzer` (diagnostic id `SHIP0001`) walks every type
  carrying `WireContractAttribute` and reports a warning on each property
  typed `decimal`.
- **The compliant contracts.** `RateQuoteContract` and `BookingChargeContract`
  both carry `[WireContract]` and represent money as `long` minor units, so
  the analyzer has nothing to flag; `WireContracts.csproj` builds with zero
  `SHIP0001` diagnostics. `InternalRateCalculation` carries no
  `[WireContract]` attribute, so its `decimal Amount` is unremarkable and the
  analyzer stays quiet.
- **Project shape.** `WireContracts.csproj` is the consuming library at the
  top level. `WireContracts.Analysis/` is a nested `netstandard2.0` project,
  the floor every Roslyn-hosting compiler can load an analyzer assembly from,
  referenced back into the consuming project with
  `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`: it
  participates in compilation as a compiler plugin, never as an assembly
  reference. `WireContracts.Tests/` is a normal nested xUnit v3 project.

**Pinning the compiler.** The .NET 10 SDK 10.0.113 hosts Roslyn 5.0.0.
`WireContracts.Analysis.csproj` pins `Microsoft.CodeAnalysis.CSharp` to that
same `5.0.0` (not NuGet-latest, which at the time of writing is newer than
the host and trips `CS9057` under warnings-as-errors) plus
`Microsoft.CodeAnalysis.Analyzers` `4.14.0`, both `PrivateAssets="all"` so
neither becomes a runtime dependency of the shipped analyzer.
`EnforceExtendedAnalyzerRules` is on, which in turn requires release
tracking for every diagnostic id (`RS2008`); `AnalyzerReleases.Shipped.md`
and `AnalyzerReleases.Unshipped.md` declare `SHIP0001` as unshipped rather
than suppressing the rule.

**What the tests prove.** `WireContracts.Tests` runs the analyzer through the
raw Roslyn compiler API, not a `*.Analyzer.Testing.Xunit` package:
`AnalyzerTestHarness` compiles a source string into an in-memory
`CSharpCompilation` using the host's trusted-platform-assembly list for
references, then calls `WithAnalyzers` and `GetAnalyzerDiagnosticsAsync`.
Each test snippet declares its own `WireContracts.WireContractAttribute`, so
every test is hermetic and never depends on the real `WireContracts`
assembly. `MoneyAsDecimalAnalyzerTests` confirms the analyzer fires exactly
once on a `decimal` property inside a wire contract, stays quiet on a
compliant `long`-minor-units contract, stays quiet on a `decimal` outside any
wire contract, reports `SHIP0001` at `Warning` severity on the offending
property's location, flags every offending property when a type has more
than one, and names both the property and the type in its message.

## Build and test

```sh
cd examples/28-roslyn-analyzers
dotnet build -c Release WireContracts.csproj
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
