---
title: "Roslyn analyzers: patterns enforced at compile time"
order: 28
layout: tutorial
sectionid: docs
part: "idiomatic-csharp"
description: "Encoding a design rule as a build error, and the concrete landing point for the AI throughline."
---

<!-- EXAMPLE (shipping domain): An analyzer enforcing that money is never a decimal in a wire contract. -->

## Learning objectives

By the end of this chapter you can:

- Describe a rule with a `DiagnosticDescriptor`: id, category, severity and message format.
- Write an `Initialize` that registers work rather than doing it, and resolve a marker symbol once per compilation.
- Walk a named type for a violation and report a diagnostic at the offending property.
- Treat severity as policy: ship a `Warning` and let the consuming project decide whether it fails the build.
- Track a new diagnostic id in `AnalyzerReleases.Unshipped.md` instead of suppressing the release-tracking rules.

## Why this matters

Money that crosses a process boundary, over HTTP, gRPC or a queue, shouldn't be a `decimal`. `decimal` has no fixed cross-platform wire representation, so a value can round-trip differently depending on the serializer and the language at the other end. The fix is an integer count of minor units such as cents. Nothing in the type system stops a new contract from using `decimal`, because it's the natural type for a money property. So the rule lives in a comment, a review checklist or a reviewer's memory.

A Roslyn analyzer moves it into the build. The compiler publishes its semantic model through `Microsoft.CodeAnalysis`, and a `DiagnosticAnalyzer` runs inside the same compilation. The rule "no `decimal` money on a `[WireContract]` type" becomes a diagnostic on every build and every keystroke in the editor, at the property that breaks it.

{% include excalidraw.html name="28-roslyn-analyzers" caption="Figure 28.1: MoneyAsDecimalAnalyzer checks every [WireContract] type and reports SHIP0001 wherever it finds a decimal money property. Contracts using integer minor units pass; types without the attribute are skipped entirely." %}

## A diagnostic as a described rule

A type opts in with a marker attribute:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class WireContractAttribute : Attribute;
```

Two contracts comply; the third type isn't a contract, so its `decimal` is fine:

```csharp
[WireContract]
public sealed record RateQuoteContract(string CarrierCode, long AmountMinorUnits, string Currency);

[WireContract]
public sealed record BookingChargeContract(long PriceMinorUnits, int Quantity);

public sealed record InternalRateCalculation(decimal Amount);
```

An analyzer declares every diagnostic it can report through `SupportedDiagnostics`. `MoneyAsDecimalAnalyzer` has one:

```csharp
public const string DiagnosticId = "SHIP0001";

private static readonly DiagnosticDescriptor Rule = new(
    DiagnosticId,
    title: "Money must not be modeled as decimal on a wire contract",
    messageFormat: "Property '{0}' on wire-contract type '{1}' uses decimal for money; use integer minor units",
    category: "ShippingContracts",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Wire-contract types should represent money as an integer count of minor units " +
        "(cents) rather than decimal, which has no fixed cross-platform wire representation.");

public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    ImmutableArray.Create(Rule);
```

| Field | Role |
| --- | --- |
| `DiagnosticId` | `SHIP0001` in build logs, editor squiggles and `.editorconfig` overrides; stable once published |
| `category` | groups shipping-contract rules |
| `messageFormat` | `{0}` and `{1}` take the property and type names, so the message locates the problem |
| `defaultSeverity`, `isEnabledByDefault` | the author's default visibility; consumers can change it |

## The Initialize contract

`Initialize` registers callbacks. It doesn't analyze anything:

```csharp
public override void Initialize(AnalysisContext context)
{
    context.EnableConcurrentExecution();
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

    context.RegisterCompilationStartAction(start =>
    {
        var marker = start.Compilation.GetTypeByMetadataName("WireContracts.WireContractAttribute");
        if (marker is null)
        {
            return;
        }

        start.RegisterSymbolAction(ctx => AnalyzeType(ctx, marker), SymbolKind.NamedType);
    });
}
```

The first two calls are promises about every callback that follows:

- **`EnableConcurrentExecution`** says the callbacks share no mutable state, so the host can analyze several files at once.
- **`ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`** skips generated code, so a `decimal` a source generator emits never trips `SHIP0001`.

Leave either out and the build fails: RS1026 flags the missing concurrent execution and RS1025 the missing generated-code setting.

{% include excalidraw.html name="28-analyzer-diagnostic-pipeline" caption="Figure 28.2: Initialize resolves the WireContractAttribute marker once per compilation, then registers a symbol action that walks every named type, reusing that resolved marker instead of looking it up again." %}

`RegisterCompilationStartAction` runs once per compilation, and an editor may feed one analyzer instance many compilations. `GetTypeByMetadataName` resolves the marker against that compilation's symbol table. If the project doesn't reference `WireContracts`, it returns `null` and nothing more is registered. Otherwise the symbol action runs for every class, struct and record, with the resolved `marker` captured in the closure so it's never looked up again.

## Walking a type for the violation

```csharp
private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol marker)
{
    var type = (INamedTypeSymbol)context.Symbol;

    var hasWireContractAttribute = type.GetAttributes()
        .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker));
    if (!hasWireContractAttribute)
    {
        return;
    }

    foreach (var member in type.GetMembers())
    {
        if (member is not IPropertySymbol { Type.SpecialType: SpecialType.System_Decimal } property)
        {
            continue;
        }

        var location = property.Locations.FirstOrDefault() ?? type.Locations.First();
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, property.Name, type.Name));
    }
}
```

Three details carry the rule:

- **`SymbolEqualityComparer.Default`**, not `==`: two symbol instances for the same attribute class aren't guaranteed to be one object.
- **`SpecialType.System_Decimal`** matches by the type's classification, so an alias or a fully qualified `System.Decimal` can't slip past a name check. The property pattern matches properties only.
- **One diagnostic per property.** A contract with two `decimal` properties gets two warnings, each located at its property.

`InternalRateCalculation` returns at the attribute check, so its `decimal Amount` is never examined.

## Severity as policy

`Rule` ships as a `Warning`. That's visible everywhere without deciding for the consumer whether the build stops. The consuming project decides:

| Consumer setting | Effect on `SHIP0001` |
| --- | --- |
| default | warning in build and editor |
| `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | fails the build, along with every other warning |
| `<WarningsAsErrors>SHIP0001</WarningsAsErrors>` | fails the build for this rule only |
| `dotnet_diagnostic.SHIP0001.severity = suggestion` in `.editorconfig` | lowered, scoped to the files the section matches |

`examples/Directory.Build.props` sets `TreatWarningsAsErrors`, so changing `RateQuoteContract.AmountMinorUnits` to `decimal` fails the build with `error SHIP0001`. None of these choices touch the analyzer: it reports one fact and the project chooses how disruptive that fact is.

## Shipping and tracking the diagnostic

`WireContracts.Analysis.csproj` targets `netstandard2.0`, the framework every Roslyn compiler host can load an analyzer from. It pins `Microsoft.CodeAnalysis.CSharp` to `5.0.0` to match the Roslyn version the SDK's compiler hosts; a newer package than the host trips CS9057 under warnings-as-errors.

`EnforceExtendedAnalyzerRules` turns on Roslyn's analyzer-authoring rules, including release tracking. RS2008 asks a project to enable tracking with two files, `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md`, and RS2000 then flags any diagnostic id missing from both. `SHIP0001` sits in the unshipped file:

```text
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SHIP0001 | ShippingContracts | Warning | Money must not be modeled as decimal on a wire contract
```

At the next release the row moves to the shipped file. Declaring the id instead of suppressing the rule keeps an auditable list of every diagnostic, when it shipped and at what severity, as a public API tracking file does for types.

## Build, run, observe

Three projects: `WireContracts.csproj` (the attribute and contracts), `WireContracts.Analysis/` (the analyzer, referenced with `OutputItemType="Analyzer"` so it runs as a compiler plugin), and `WireContracts.Tests/`.

```sh
cd examples/28-roslyn-analyzers
dotnet test
```

All 6 tests pass. They drive the analyzer through the Roslyn API directly: parse a source string into a `CSharpCompilation`, attach the analyzer with `compilation.WithAnalyzers(...)`, and collect `GetAnalyzerDiagnosticsAsync()`. Each snippet declares its own `WireContractAttribute`, so the tests don't depend on the real library. `WireContracts.csproj` itself builds with no `SHIP0001`. Two experiments:

- **Make a contract non-compliant.** Change `long AmountMinorUnits` to `decimal` in `Contracts.cs`. The build fails with `error SHIP0001: Property 'AmountMinorUnits' on wire-contract type 'RateQuoteContract' uses decimal for money`.
- **Drop the release entry.** Empty `AnalyzerReleases.Unshipped.md`. The analyzer project fails with RS2000: `Rule 'SHIP0001' is not part of any analyzer release`.

## When not to use it

- **A rule with one occurrence.** One wire contract and one team that reviews every change to it don't need an analyzer project with its own target framework, pinned Roslyn and tests. A comment in the type covers it. An analyzer pays off once the rule must hold across many types, contributors or pull requests.
- **A rule wider than the risk.** Flagging every `decimal` in the codebase would hit `InternalRateCalculation.Amount` and every in-process calculation. Constant false positives lead to a project-wide `NoWarn`. Scope the check to a marker attribute.
- **Matching by naming convention.** Treating types named `*Contract` or `*Dto` as wire types misses renamed contracts and flags types that never cross a boundary. An explicit attribute, resolved by its metadata name, survives renames.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a wire contract.** "Add a contract for rate quotes" leaves the money type open, and `decimal` is the default most C# code uses for money. State it:

> Add a `[WireContract]` sealed record for rate quotes. Represent money as a `long` count of minor units named `AmountMinorUnits`, never `decimal`. The build runs the `SHIP0001` analyzer with warnings as errors.

If the reply uses `decimal`, the build fails with `SHIP0001` before review.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Write your first analyzer and code fix](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix): the Roslyn SDK tutorial for the `DiagnosticAnalyzer` and `Initialize` shape.
- Microsoft Learn, [DiagnosticAnalyzer Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticanalyzer): `Initialize`, `EnableConcurrentExecution` and `ConfigureGeneratedCodeAnalysis`.
- Microsoft Learn, [DiagnosticDescriptor Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnosticdescriptor): the descriptor fields, and how `defaultSeverity` and `isEnabledByDefault` relate to a consumer's `.editorconfig` override.
- Roslyn analyzers repository, [Analyzer Release Tracking](https://github.com/dotnet/roslyn-analyzers/blob/main/docs/Analyzer%20Release%20Tracking.md): RS2008, RS2000 and the `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` format.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
