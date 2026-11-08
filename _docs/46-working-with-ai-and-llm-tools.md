---
title: "Working with AI and LLM tools"
order: 46
layout: tutorial
sectionid: docs
part: "putting-it-together"
description: "Reviewing pattern-shaped LLM output, with the analyzer from the idiomatic-C# part as the enforcement mechanism."
---

<!-- EXAMPLE (shipping domain): Reviewing LLM-generated pattern code in the shipping domain. -->

## Learning objectives

By the end of this chapter you can:

- Name the "Strategy tower with one strategy" smell, and explain why it compiles cleanly and still deserves a second look.
- Trace `SingleImplementationStrategyAnalyzer` from `CompilationStart` through `SymbolAction` to `CompilationEnd`, and say why counting implementers can't happen in one symbol callback.
- State the analyzer's naming-scope guard and its four firing cases: fires on one implementation, quiet on two, quiet on a non-`Strategy` name, quiet on none.
- Contrast the single-implementation `IRatingStrategy` an assistant drafts first with the corrected `RatingReview`, where `StandardRatingStrategy` and `ExpressRatingStrategy` are two real shapes.
- Drive an analyzer through the raw Roslyn compiler API on a snippet compiled in memory.

## Why this matters

An assistant drafting C# reaches for familiar shapes, and a Strategy interface behind a single implementation is one of the most common. The interface compiles, its one class compiles, and every test against it passes. Nothing in a normal build says the interface exists to make a second pricing formula, routing rule or surcharge swappable, and that no second one exists. Today the abstraction is pure cost: an extra file, an extra layer, two types to open to understand one formula. It pays only when a real second case arrives, and until then it looks like well-factored code, which is why it gets waved through review.

Catching it by eye doesn't scale. A reviewer does the same count on every diff that proposes a pricing engine or a surcharge calculator, and a tired reviewer misses the fifth one as reliably as they catch the first. Turn the review rule into something the build runs on every compilation: a Roslyn analyzer that counts the classes implementing each `*Strategy` interface and warns when the count is exactly one. The analyzer doesn't know whether the interface should stay. It knows the shape is worth a second look, and it puts that question in front of a person at build time.

{% include excalidraw.html name="46-working-with-ai-and-llm-tools" caption="Figure 46.1: An assistant proposes pattern-shaped code, the SHIP0002 analyzer flags a single-implementation Strategy, and a human decides whether to collapse the abstraction or justify it before the change merges." %}

## The smell: a Strategy interface with one implementation

A Strategy interface exists to hold more than one shape behind a stable call site. Asked to price a freight booking, an assistant often produces the right vocabulary: an interface named for the concept, one implementation and a caller coded against the interface. Each choice is good practice on its own. With one implementation, together they describe an abstraction that hasn't done its job yet. Nothing is wrong with the code; what's missing is the second shape that would make the indirection pay, and a passing build doesn't mention it.

{% include excalidraw.html name="46-single-implementation-strategy" caption="Figure 46.2: An IRatingStrategy with only StandardRatingStrategy behind it trips SHIP0002. Adding ExpressRatingStrategy gives the interface a second distinct shape, and the analyzer falls silent." %}

In the shipping domain, a quote depends on service level: ground bills differently from express air. Working from a ticket that mentions one service level, an assistant commonly writes `IRatingStrategy` with a single `StandardRatingStrategy`, because that's what a pluggable calculation looks like however many calculations exist. It reviews cleanly: small interface, focused implementation, a caller on the abstraction. The gap is that express service, the case the interface exists for, isn't written, and nothing in the review says whether it's coming next week or never.

## SHIP0002: a compilation-wide count behind a build warning

`SingleImplementationStrategyAnalyzer` lives in `RatingReview.Analysis`, a project that compiles into a Roslyn diagnostic analyzer and runs inside every build of the consuming library. It declares one diagnostic, `SHIP0002`, in the `ShippingReview` category at `DiagnosticSeverity.Warning`:

```csharp
private static readonly DiagnosticDescriptor Rule = new(
    DiagnosticId,
    title: "Strategy interface has a single implementation",
    messageFormat: "Strategy interface '{0}' has a single implementation '{1}'; confirm the abstraction earns its place",
    category: "ShippingReview",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "A Strategy interface with only one implementation is a common shape for " +
        "pattern-shaped output from an LLM: review it before it merges to confirm the " +
        "abstraction is pulling its weight, rather than anticipating a second strategy " +
        "that never arrives.",
    customTags: WellKnownDiagnosticTags.CompilationEnd);
```

"How many classes implement this interface?" can't be answered from one type. A symbol callback sees one class at a time and can't know whether a second implementation sits in a file not yet visited. So the analyzer splits the work across two registrations inside one `CompilationStartAction`: a `SymbolAction` per named type, and a `CompilationEndAction` after every type has been seen.

```csharp
context.RegisterCompilationStartAction(start =>
{
    var candidateInterfaces = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);
    var implementersByInterface = new ConcurrentDictionary<ISymbol, ConcurrentBag<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

    start.RegisterSymbolAction(
        ctx => CollectType(ctx, candidateInterfaces, implementersByInterface),
        SymbolKind.NamedType);

    start.RegisterCompilationEndAction(
        end => ReportSingleImplementations(end, candidateInterfaces, implementersByInterface));
});
```

`CollectType` runs once per type. A candidate Strategy interface gets recorded; a class or struct walks `AllInterfaces` and adds itself to each candidate's implementer list. Both dictionaries are concurrent and keyed by `ISymbol` with `SymbolEqualityComparer.Default`, because Roslyn analyzes symbols concurrently by default and a plain `Dictionary<TKey, TValue>` isn't safe under that. `ReportSingleImplementations` runs once, at the end, and reports `SHIP0002` for every candidate with exactly one implementer. The `customTags: WellKnownDiagnosticTags.CompilationEnd` on the descriptor is required: rule `RS1037` makes a diagnostic that can only be judged after the whole compilation say so, and checks the tag against the registration.

The naming-scope guard keeps the rule on the one shape it targets:

```csharp
private static bool IsStrategyInterface(INamedTypeSymbol type) =>
    type.TypeKind == TypeKind.Interface && type.Name.EndsWith(StrategySuffix, StringComparison.Ordinal);
```

An `ISlotAllocator` with one implementation isn't a candidate at all. The rule only questions interfaces whose names declare, by the project's convention, that they hold interchangeable strategies. A single-implementation interface isn't wrong in general, and narrowing the rule keeps it off the small interfaces people extract for reasons unrelated to Strategy.

The diagnostic lands on the interface's own identifier:

```csharp
var location = interfaceSymbol.Locations.FirstOrDefault(static location => location.IsInSource)
    ?? Location.None;

context.ReportDiagnostic(Diagnostic.Create(Rule, location, interfaceSymbol.Name, implementation.Name));
```

The squiggle sits on `IRatingStrategy`, not `StandardRatingStrategy`, because the decision is about the abstraction. The message carries both names, so the reader doesn't have to go find the sole implementation.

## The corrected sample: two strategies, nothing to flag

`RatingReview.csproj`, the library the analyzer protects, holds the reviewed feature: `IRatingStrategy` with two implementations, one per service level.

```csharp
public interface IRatingStrategy
{
    decimal Quote(BookingRequest request);
}

public sealed class StandardRatingStrategy : IRatingStrategy
{
    private const decimal PerKilogramRate = 2.35m;
    private const decimal InsuranceRate = 0.004m;

    public decimal Quote(BookingRequest request) =>
        request.WeightKilograms * PerKilogramRate + request.DeclaredValue * InsuranceRate;
}

public sealed class ExpressRatingStrategy : IRatingStrategy
{
    private const decimal PerKilogramRate = 6.10m;
    private const decimal HandlingFee = 18.00m;
    private const decimal InsuranceRate = 0.009m;

    public decimal Quote(BookingRequest request) =>
        request.WeightKilograms * PerKilogramRate + HandlingFee + request.DeclaredValue * InsuranceRate;
}
```

| Strategy | Per kg | Handling fee | Insurance on declared value |
| --- | --- | --- | --- |
| `StandardRatingStrategy` | `2.35m` | none | `0.004m` |
| `ExpressRatingStrategy` | `6.10m` | `18.00m` | `0.009m` |

Standard is the ground formula the first draft had. Express is a second shape: a higher per-kilogram rate for faster transit, a handling fee ground never charges, and a steeper insurance rate for the higher-value freight express tends to carry. These are two pricing formulas the system has to support, the case `IRatingStrategy` was named for. With two implementers, `ReportSingleImplementations` finds no match and nothing fires. The analyzer never objects to the interface, only to the gap between what it promises and how many shapes back it.

`SHIP0002` can't tell a real second strategy from a class added to quiet it, one that delegates to the first formula or hardcodes a value nobody asked for. Silencing the warning takes minutes either way. The analyzer forces the choice into the open; it doesn't make it. Two fixes are legitimate:

- add `ExpressRatingStrategy`, because express service is a real requirement;
- collapse `IRatingStrategy` into a single `RatingCalculator` class, because no second service level is planned.

Only one is right for a given booking system, and the analyzer's job ends at saying a decision is owed.

## Testing the analyzer with the raw Roslyn compiler API

`AnalyzerTestHarness` drives the analyzer without a packaged analyzer-testing library, using the compiler APIs an analyzer already runs inside: `CSharpSyntaxTree.ParseText`, `CSharpCompilation.Create`, then `WithAnalyzers` and `GetAnalyzerDiagnosticsAsync`.

{% include excalidraw.html name="46-analyzer-as-the-gate" caption="Figure 46.3: AnalyzerTestHarness compiles a snippet and runs the analyzer between compilation and its diagnostics. A single-implementation Strategy makes SHIP0002 fire; a two-implementation one leaves it quiet." %}

```csharp
var compilation = CSharpCompilation.Create(
    assemblyName: "RatingReview.Tests.Snippet",
    syntaxTrees: [syntaxTree],
    references: FrameworkReferences,
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new SingleImplementationStrategyAnalyzer());
var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
```

References come from the running host's trusted-platform-assembly list rather than a reference-assembly NuGet package, so each snippet compiles against the runtime already hosting the tests, with nothing more to restore. Each snippet is a complete compilation unit, a whole `namespace RatingReview { ... }` block with its own interface and implementations, so none depends on the real `RatingReview` assembly or leaks types into another.

The analyzer's behavior across snippets:

| Snippet | Result |
| --- | --- |
| `ISurchargeStrategy` with one `FuelSurchargeStrategy` | One diagnostic |
| `IDemurrageStrategy` with a flat and a tiered implementation | Quiet |
| `ISlotAllocator` with one implementation | Quiet: the name is out of scope |
| A Strategy interface nothing implements | Quiet: not the smell |
| `IRoutingStrategy` with one implementation | Id `SHIP0002`, severity `Warning`, location text `IRoutingStrategy` |
| `IConsolidationStrategy` with `VolumeConsolidationStrategy` | Message names both |
| Two unrelated single-implementation Strategy interfaces | Both flagged; per-interface counts don't interfere |

Using the compiler's own APIs keeps the checks grounded in what the analyzer does in a real build: parse, compile, run analyzers on the finished compilation, and read the diagnostics that come back. It's the sequence `dotnet build` runs, called directly.

## Reviewing AI output as a human-owned gate

None of this makes review automatic. `SHIP0002` answers one structural question, how many classes implement an interface named for a Strategy, and answers it the same way for hand-written and generated code. What changes is where the rule lives:

| Rule written as | Behavior |
| --- | --- |
| A style-guide sentence: "no Strategy interface until two real implementations" | Remembered, applied and recounted by eye on every diff; degrades under fatigue, deadlines and unfamiliar code |
| An analyzer | Runs on every compilation, flags the shape whoever wrote it and however persuasive the code reads, and shows up in the build output |

That shift fits how fast an assistant produces plausible abstractions. A single-implementation Strategy isn't a bug. It compiles, passes the tests for its one implementation, and reads as careful design in a skimmed diff. Encoded as a build check, it gets flagged whether or not the reviewer looked closely at that file. The recipe for turning a design rule into a compiler diagnostic is the same for any shape: name the smell precisely enough that the analyzer fires only on it, report where a developer needs to look, and let the build be what never forgets to ask.

The decision after a `SHIP0002` warning still belongs to a person. The analyzer can't know whether express service lands next sprint or was never requested; it counts implementers. A developer has the ticket, the planned pricing rules and the judgment to tell a real second strategy from a class written to quiet a warning. Catching the shape is mechanical and belongs to the tool. Choosing to collapse the abstraction or build the second strategy in good faith stays with a person every time.

## Build, run, observe

```sh
cd examples/46-working-with-ai-and-llm-tools
dotnet test
```

All 7 analyzer tests pass. `dotnet build` is also green with zero warnings under warnings-as-errors, because the corrected `IRatingStrategy` has two implementations and never trips `SHIP0002` in its own compilation.

## For deeper coverage

- Gamma, Helm, Johnson, Vlissides, [*Design Patterns: Elements of Reusable
  Object-Oriented Software*](https://www.oreilly.com/library/view/design-patterns-elements/0201633612/)
  (Addison-Wesley, 1994): the original Strategy pattern, defined for a
  world where "has more than one implementation" was an assumption baked
  into why a team reached for the pattern in the first place.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html),
  2nd ed. (Addison-Wesley, 2018): the discipline of right-sizing an
  abstraction to the cases it serves, and of collapsing one back
  down when the second case it was built for never arrives.
- Microsoft Learn, [.NET Compiler Platform SDK](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/): the entry point for the Roslyn analyzer and source generator APIs `SingleImplementationStrategyAnalyzer` is built on.
- Microsoft Learn, [How to write a Roslyn analyzer](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/analyzers-overview): the `DiagnosticAnalyzer` and `DiagnosticDescriptor` shapes, symbol actions, and compilation-wide analysis `SingleImplementationStrategyAnalyzer` follows.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
