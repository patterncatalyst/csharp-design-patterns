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

- Describe the contract `DiagnosticAnalyzer` and `DiagnosticDescriptor`
  establish between an analyzer and the compiler, and name the pieces of
  `MoneyAsDecimalAnalyzer`'s `Rule` (id `SHIP0001`, category
  `ShippingContracts`, severity, message format) that a build log or an IDE
  surfaces to a developer.
- Explain what `Initialize` registers before an analyzer examines any code,
  and why `EnableConcurrentExecution` and `ConfigureGeneratedCodeAnalysis`
  are called unconditionally at the top of the method rather than inside a
  registered callback.
- Trace how `RegisterCompilationStartAction` resolves
  `WireContracts.WireContractAttribute` once per compilation through
  `GetTypeByMetadataName`, and explain why `AnalyzeType` never resolves that
  symbol again once `RegisterSymbolAction` is registered for
  `SymbolKind.NamedType`.
- Explain severity-as-policy: why `MoneyAsDecimalAnalyzer.Rule` ships at
  `DiagnosticSeverity.Warning` rather than `Error`, and how a consuming
  project's own `TreatWarningsAsErrors` setting turns `SHIP0001` into a build
  failure without the analyzer author hard-coding that outcome.
- Read `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` and
  explain what `RS2008` release tracking requires of a new diagnostic id, and
  why declaring `SHIP0001` unshipped is the idiomatic alternative to
  suppressing the rule.

## Why this matters

A wire contract carries one invariant that no property type in C# enforces
on its own: money that crosses a process boundary, over HTTP, gRPC, or a
message queue, must never be a `decimal`. `decimal` has no fixed
cross-platform wire representation, so the same value can round-trip
differently depending on the serializer and the language on the other end.
The fix is simple, an integer count of minor units such as cents, but
nothing in the type system stops a new contract from reaching for `decimal`
instead, because `decimal` is exactly the type a money-shaped property
reads as natural to reach for. Today that rule lives wherever a team writes
it down: a comment on the base contract type, a line in a review checklist,
a note a reviewer has to remember to check on every pull request that adds
a new wire type.

A Roslyn analyzer moves that rule out of memory and into the build. The C#
compiler Microsoft ships is not a closed box that turns source into
binaries and discards everything it learned along the way; it publishes its
own semantic model, the same information it uses to type-check a program,
through the `Microsoft.CodeAnalysis` API, and a project can register a
`DiagnosticAnalyzer` that runs inside the same compilation pass. The rule
in this chapter, no `decimal` money on a `[WireContract]` type, becomes a
diagnostic the compiler reports on every build and every keystroke in an
editor, at the exact property that violates it, rather than a fact a
reviewer has to recall.

{% include excalidraw.html name="28-roslyn-analyzers" caption="Figure 28.1: MoneyAsDecimalAnalyzer inspects every type carrying [WireContract] during compilation. RateQuoteContract and BookingChargeContract pass because both model money as long minor units; InternalRateCalculation carries no [WireContract] attribute, so its decimal Amount is never examined. A [WireContract] type with a decimal money property is where the analyzer reports SHIP0001, at the offending property's own location." %}

## A diagnostic as a described rule

The example behind this chapter lives in `examples/28-roslyn-analyzers`.
`WireContractAttribute` is the marker a type opts into the rule with:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class WireContractAttribute : Attribute;
```

`Contracts.cs` shows both sides of the rule in the same file. `RateQuoteContract`
and `BookingChargeContract` each carry `[WireContract]` and model money as a
`long` count of minor units, so the analyzer has nothing to flag on either
one. `InternalRateCalculation` carries no `[WireContract]` attribute at all,
so its `decimal Amount` is unremarkable, an in-process value that never
crosses a boundary:

```csharp
[WireContract]
public sealed record RateQuoteContract(string CarrierCode, long AmountMinorUnits, string Currency);

[WireContract]
public sealed record BookingChargeContract(long PriceMinorUnits, int Quantity);

public sealed record InternalRateCalculation(decimal Amount);
```

`MoneyAsDecimalAnalyzer` is the `DiagnosticAnalyzer` that enforces the
distinction those three records already draw. A `DiagnosticAnalyzer`
describes every diagnostic it can report before it reports any of them,
through a `SupportedDiagnostics` property, and `MoneyAsDecimalAnalyzer`
supports exactly one, built from a single `DiagnosticDescriptor`:

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

`DiagnosticId` is the short code, `SHIP0001`, that shows up in a build log, an
IDE squiggle, and an `.editorconfig` severity override alike, so it has to be
stable once published. `category` groups the rule with any other
shipping-contract diagnostics the project adds later. `messageFormat` holds
two placeholders, `{0}` and `{1}`, that a reported diagnostic fills with the
offending property's name and its declaring type's name, so a developer
reading the warning sees exactly which property and which contract without
opening the file first. `defaultSeverity` and `isEnabledByDefault` are the
analyzer's own opinion about how visible the rule should be out of the box,
a decision the next section returns to directly.

## The Initialize contract

Every `DiagnosticAnalyzer` implements one method the host compiler calls
once, `Initialize`, and that method's job is registration, not analysis:

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

`EnableConcurrentExecution` and `ConfigureGeneratedCodeAnalysis` run first
and unconditionally, because both are promises the analyzer makes about
every callback it is about to register, not configuration for one of them.
`EnableConcurrentExecution` tells the host compiler this analyzer's
callbacks hold no mutable shared state across files, so it is safe to run
them on several source files at once rather than one at a time, which is
how a large solution keeps analysis fast. `ConfigureGeneratedCodeAnalysis(
GeneratedCodeAnalysisFlags.None)` opts the analyzer out of examining
compiler- and tool-generated code entirely, so a type a source generator
emits never trips `SHIP0001` on a property the generator itself produced,
even if that property happens to be a `decimal`.

{% include excalidraw.html name="28-analyzer-diagnostic-pipeline" caption="Figure 28.2: Initialize registers a RegisterCompilationStartAction that resolves the marker symbol for WireContracts.WireContractAttribute once through GetTypeByMetadataName and returns early if the symbol is missing. Once resolved, RegisterSymbolAction runs AnalyzeType for every named type in the compilation, closing over the already-resolved marker symbol rather than looking it up again." %}

`RegisterCompilationStartAction` is where the real work begins, and it runs
once per compilation rather than once per analyzer instance, which matters
because a single `MoneyAsDecimalAnalyzer` instance can analyze several
compilations across a solution's lifetime in an IDE. Inside that callback,
`start.Compilation.GetTypeByMetadataName("WireContracts.WireContractAttribute")`
resolves the marker attribute by its fully qualified metadata name against
this specific compilation's symbol table, the same symbol table the
compiler itself uses to bind the attribute when a type writes `[WireContract]`.
If a project never references `WireContracts` at all, `GetTypeByMetadataName`
returns `null`, and the callback returns immediately rather than registering
anything further: an analyzer with nothing to find has nothing to walk.
Once the marker resolves, `RegisterSymbolAction(ctx => AnalyzeType(ctx,
marker), SymbolKind.NamedType)` registers a second callback that the host
compiler calls once for every named type in the compilation, a class, a
struct, or a record alike, each call carrying the already-resolved `marker`
symbol as a closure variable so `AnalyzeType` never pays the cost of
resolving it again.

## Walking a type for the violation

`AnalyzeType` is the callback `RegisterSymbolAction` dispatches to for every
named type, and its first job is deciding whether this particular type is
one the rule applies to at all:

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

`type.GetAttributes().Any(...)` checks every attribute the type carries
against the resolved `marker` symbol, and that comparison runs through
`SymbolEqualityComparer.Default` rather than `==` or `.Equals`, because two
`INamedTypeSymbol` instances describing the same attribute class are not
guaranteed to be the same object reference; `SymbolEqualityComparer` is the
Roslyn API's own answer to comparing symbols for identity across a
compilation. A type that does not carry `[WireContract]` returns
immediately, exactly the outcome `InternalRateCalculation` produces: its
`decimal Amount` is never even reached, because the method exits before the
`foreach` loop that would have examined it. For a type that does carry the
attribute, `GetMembers()` walks every member, and the pattern
`IPropertySymbol { Type.SpecialType: SpecialType.System_Decimal }` matches
only a property, not a field or a method, whose declared type is `decimal`
by its special-type classification rather than a string name comparison
that a type alias or a fully qualified reference could dodge. Each matching
property yields one diagnostic, so `BookingChargeContract` with two
offending `decimal` properties would produce two separate `SHIP0001`
warnings rather than one; the `location` fallback, `property.Locations.
FirstOrDefault() ?? type.Locations.First()`, points the squiggle at the
property itself in the ordinary case and falls back to the type's own
location only for the edge case of a property with no recorded source
location at all. `context.ReportDiagnostic` fills `Rule`'s `{0}` and `{1}`
placeholders with `property.Name` and `type.Name`, so the message a
developer reads names both without them opening the file.

## Severity as policy

`MoneyAsDecimalAnalyzer.Rule` ships with `defaultSeverity:
DiagnosticSeverity.Warning`, not `Error`. That choice is itself a form of
policy, separate from the detection logic `AnalyzeType` carries out: a
`Warning` is visible in every build log and every IDE without being able,
on its own, to stop a build that a consuming project has not opted into
stopping. This book's own examples opt into exactly that escalation.
`examples/Directory.Build.props` sets `TreatWarningsAsErrors` to `true` for
every project under `examples/`, including `WireContracts.csproj`, the
library `RateQuoteContract`, `BookingChargeContract`, and
`InternalRateCalculation` live in. Because every contract in that project
already models money as minor units, `SHIP0001` never fires there and the
build stays clean, but if a new wire contract were added with a `decimal`
money property, the same `Warning` this analyzer reports would fail that
build outright under the repository's existing settings, with no change to
the analyzer itself.

That is the shape severity-as-policy takes in practice: an analyzer author
sets a default severity that is visible without presuming how strict every
consumer wants to be, and a consuming project decides the rest through its
own build settings. A team that wants `SHIP0001` to fail the build can set
`TreatWarningsAsErrors` globally, as this book's examples already do, or
scope the escalation to just this rule with `<WarningsAsErrors>SHIP0001
</WarningsAsErrors>`. A team not yet ready to fail builds on it can leave
the default `Warning` in place and still see every violation flagged. A
team that disagrees with the rule for one file can lower it again with
`dotnet_diagnostic.SHIP0001.severity = suggestion` in an `.editorconfig`,
scoped to that file alone. None of those choices touch `MoneyAsDecimalAnalyzer.cs`;
the analyzer reports one fact, a `decimal` property on a `[WireContract]`
type, and leaves how disruptive that fact is to the project that consumes
it.

## Shipping and tracking the diagnostic

`WireContracts.Analysis.csproj`, the project `MoneyAsDecimalAnalyzer` lives
in, targets `netstandard2.0` rather than the `net10.0` every other example
in this book targets, because `netstandard2.0` is the floor every version
of the Roslyn-hosting compiler can load an analyzer assembly from. It also
pins `Microsoft.CodeAnalysis.CSharp` to `5.0.0`, matching the Roslyn version
the .NET 10 SDK's own compiler hosts, rather than floating to whatever is
newest on NuGet; a newer Roslyn package than the host compiler trips
`CS9057` under warnings-as-errors, so the analyzer project's own dependency
stays pinned to the version it will run inside.

`EnforceExtendedAnalyzerRules` is set to `true` on the same project, which
turns on a set of analyzer-authoring rules Roslyn ships for checking an
analyzer against itself, among them the requirement this chapter's last
learning objective names: `RS2008`, release tracking for every diagnostic
id a project's analyzers report. `RS2008` requires every `DiagnosticId` an
analyzer supports to appear in one of two files, `AnalyzerReleases.
Shipped.md` or `AnalyzerReleases.Unshipped.md`, each a small Markdown table:

```text
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SHIP0001 | ShippingContracts | Warning | Money must not be modeled as decimal on a wire contract
```

`SHIP0001` is declared in `AnalyzerReleases.Unshipped.md`, the file for a
diagnostic id that has not yet gone out in a tagged release of the analyzer
package, while `AnalyzerReleases.Shipped.md` stays ready for the next
version bump to move the entry into. Declaring the id this way, rather than
suppressing `RS2008` for the project, keeps an auditable inventory of every
diagnostic the analyzer has ever introduced, when it shipped, and at what
severity, the same kind of change-tracking discipline a public API surface
gets from its own tracking file. A new rule added to this analyzer later
would follow the identical path: a new row in `AnalyzerReleases.Unshipped.md`
before the next release, not a suppressed warning.

## Build, run, observe

The example lives in `examples/28-roslyn-analyzers`, split across three
projects: `WireContracts.csproj`, the consuming library holding
`WireContractAttribute` and the three contract records; `WireContracts.
Analysis/`, the `netstandard2.0` analyzer project referenced back into the
first with `OutputItemType="Analyzer"` so it participates in compilation as
a compiler plugin rather than an assembly dependency; and `WireContracts.
Tests/`, an ordinary xUnit v3 project. It is standalone-testable from its
own directory:

```sh
cd examples/28-roslyn-analyzers
dotnet test
```

Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 6
  failed: 0
  succeeded: 6
  skipped: 0
  duration: 1s 457ms
```

`WireContracts.Tests` drives `MoneyAsDecimalAnalyzer` through the raw
Roslyn compiler API rather than a third-party analyzer-testing package.
`AnalyzerTestHarness` parses a source string into a `CSharpCompilation`,
using the running host's trusted-platform-assembly list for references, then
calls `compilation.WithAnalyzers(...)` and `GetAnalyzerDiagnosticsAsync()`
to collect whatever the analyzer reports. Each test snippet declares its
own `WireContracts.WireContractAttribute` inline, so every test is
hermetic and compiles without depending on the real `WireContracts`
assembly at all. The six cases cover both sides of the rule and the shape
of what gets reported: a `decimal` property inside a `[WireContract]` type
fires exactly once; a compliant `long`-minor-units contract stays quiet; a
`decimal` outside any `[WireContract]` type stays quiet; a reported
diagnostic carries `SHIP0001` at `Warning` severity and a location inside
the offending property; a type with two offending properties produces two
separate diagnostics; and a reported message names both the property and
the type it belongs to.

The consuming library, `WireContracts.csproj`, builds with zero `SHIP0001`
diagnostics of its own, because `RateQuoteContract` and
`BookingChargeContract` are already compliant and `InternalRateCalculation`
carries no `[WireContract]` attribute to trigger the check. Every
fire-on-bad case in this example lives in a test snippet rather than in the
shipped library, which is the point: the analyzer proves it catches the
violation before any real contract in the codebase has the chance to
introduce one.

## When not to use it

An analyzer costs more than a review comment when the rule it checks has no
second occurrence to guard against. A project with a single wire-contract
type and one team that already reads every pull request touching it gets
nothing an analyzer project, its own `netstandard2.0` target, its own
pinned Roslyn version, and its own test suite, would pay back; a one-line
reminder in that type's own file or a note in a review template covers the
same ground for less ongoing maintenance. An analyzer project makes sense
once a rule has to hold across many types, many contributors, or many
pull requests a single reviewer cannot track by memory, the situation a
shipping codebase with a growing set of wire contracts is already in.

A rule that reaches too far past what it needs to check turns
into friction rather than protection. If `MoneyAsDecimalAnalyzer` flagged
every `decimal` property anywhere in a codebase instead of only the ones on
a `[WireContract]` type, it would fire on `InternalRateCalculation.Amount`
and on every other legitimate in-process calculation a shipping system
performs, and a team facing constant false positives reaches for blanket
suppression, a project-wide `NoWarn`, rather than fixing each one. Scoping
the check to a marker attribute keeps the rule narrow enough that every
diagnostic it reports is one worth reading.

The marker attribute itself is also the reason this analyzer avoids a
subtler failure mode: recognizing a wire contract by name instead of by an
explicit opt-in. An analyzer that matched type names ending in `Contract`
or `Dto` would misclassify a type renamed away from that convention, miss a
new contract type with a different name, and flag a type that happens to
match the pattern without ever crossing a boundary. `[WireContract]` is an
explicit, renamable-safe signal a type's author adds by intent, and
`GetTypeByMetadataName` resolving it by its fully qualified name is immune
to a naming convention changing underneath it.

> **AI sidebar.** Asked to add a field to a wire contract, or to sketch one
> from a description, an AI pairing partner will often reach for `decimal`
> on a money-shaped property, because `decimal` is the type every other
> money value in a typical C# codebase already uses and nothing about the
> request signals that this particular type crosses a process boundary.
> That is exactly the pattern `SHIP0001` exists to catch: a generated
> `RateQuoteContract`-shaped record with a `decimal Amount` fails the build
> the moment it is marked `[WireContract]`, under this book's own
> `TreatWarningsAsErrors` setting, regardless of whether a person or an
> assistant wrote the property. Check a generated wire-contract type
> against the two that pass in this chapter's example: money travels as a
> `long` count of minor units, `AmountMinorUnits` or `PriceMinorUnits`,
> never as a `decimal`, and the analyzer is the thing that notices if a
> suggestion drifted back to the familiar type before anyone has to catch
> it by hand.

## For deeper coverage

- Microsoft Learn, [Write your first analyzer and code fix](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix): the Roslyn SDK's own tutorial for the `DiagnosticAnalyzer` and `Initialize` shape `MoneyAsDecimalAnalyzer` follows.
- Microsoft Learn, [DiagnosticAnalyzer Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticanalyzer): the authority for `Initialize`, `EnableConcurrentExecution`, and `ConfigureGeneratedCodeAnalysis`.
- Microsoft Learn, [DiagnosticDescriptor Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnosticdescriptor): the authority for the fields `Rule` sets, including how `defaultSeverity` and `isEnabledByDefault` differ from a severity a consuming project's `.editorconfig` can still override.
- Roslyn analyzers repository, [Analyzer Release Tracking](https://github.com/dotnet/roslyn-analyzers/blob/main/docs/Analyzer%20Release%20Tracking.md): the authority for `RS2008` and the `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` format `SHIP0001` follows.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).

