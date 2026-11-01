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

- Name the "Strategy tower with one strategy" smell: a Strategy interface an
  assistant drafted with exactly one implementing class, and explain why that
  shape compiles cleanly while still being worth a second look.
- Read `SingleImplementationStrategyAnalyzer` and trace its
  `CompilationStart` to `SymbolAction` to `CompilationEnd` shape, including
  why counting implementers across a whole compilation cannot happen from any
  single symbol callback.
- Explain the analyzer's naming-scope guard (an interface candidate only if
  its name ends in `Strategy`) and state the four cases
  `SingleImplementationStrategyAnalyzerTests` proves: fires on one
  implementation, quiet on two, quiet on a non-`Strategy` name, quiet on
  zero implementations.
- Contrast the single-implementation `IRatingStrategy` an assistant drafts
  first against the corrected `RatingReview` consumer, where
  `StandardRatingStrategy` and `ExpressRatingStrategy` give the same
  interface two distinct, real shapes to serve.
- Describe why `AnalyzerTestHarness` drives the analyzer through the raw
  Roslyn compiler API rather than a packaged testing harness, and what that
  buys a self-contained snippet compiled entirely in memory.

## Why this matters

An assistant drafting C# reaches for familiar shapes, and a Strategy
interface sitting behind a single implementation is one of the most common.
The interface compiles. The one class implementing it compiles. Every test
written against the interface passes. Nothing in a normal build points out
that the interface exists to make a second rate-pricing formula, a second
routing rule, a second surcharge calculation swappable, and that no second
formula has shown up yet. The abstraction is pure cost today: an extra file,
an extra layer of indirection, a reader who has to open two types to
understand what one formula does. The chance that it pays for itself only
arrives the day a real second case needs to be added, and until then the
interface looks exactly like well-factored code, which is what makes it easy
to wave through review.

Catching this by habit does not scale. A reviewer scanning a diff for a
Strategy interface with one implementation is doing the same count, by
eye, every time an assistant proposes a pricing engine, a routing rule, or a
surcharge calculator, and a tired reviewer on a Friday afternoon is going to
miss the fifth one as reliably as they catch the first. The fix is not a
sharper eye. It is turning the review rule itself into something the build
runs automatically on every compilation, the same way a compiler enforces a
missing semicolon: a Roslyn analyzer that walks the symbols in a
compilation, counts how many classes implement each `*Strategy` interface,
and reports a warning the moment that count sits at exactly one. The
analyzer does not know whether the interface should stay or go. It knows
only that the shape is worth a second look, and it puts that judgment in
front of a human at build time rather than leaving it to whoever happens to
notice during code review.

{% include excalidraw.html name="46-working-with-ai-and-llm-tools" caption="Figure 46.1: An assistant proposes pattern-shaped code, the SHIP0002 analyzer flags a single-implementation Strategy, and a human decides whether to collapse the abstraction or justify it before the change merges." %}

## The smell: a Strategy interface with one implementation

A Strategy interface is supposed to hold more than one shape. The pattern's
entire value is swapping one implementation for another behind a stable
call site, so a caller never needs to know which concrete pricing formula,
routing rule, or surcharge calculation it is talking to. An assistant asked
to price a freight booking will often produce exactly the right
vocabulary, an interface named for the concept it abstracts, one
implementation that satisfies it, and a caller coded against the interface
rather than the concrete class. Every one of those choices is good practice
in isolation. Put together, with only one implementation behind the
interface, they describe an abstraction that has not yet done the job it
was built for. Nothing about the code is wrong. The interface type-checks,
the implementation satisfies it, and a caller coded against `IRatingStrategy`
rather than a concrete class is, on its own, the right habit. What is
missing is the second shape that would make the indirection earn its
keep, and nothing about a passing build says so.

{% include excalidraw.html name="46-single-implementation-strategy" caption="Figure 46.2: An IRatingStrategy with only StandardRatingStrategy behind it trips SHIP0002. Adding ExpressRatingStrategy gives the interface a second distinct shape, and the analyzer falls silent." %}

The shipping domain makes the smell concrete. A freight booking needs a
quote, and a quote depends on the service level a shipper chose: ground
service bills differently from express air. An assistant drafting the first
version of this feature, working from the one service level mentioned in a
ticket, commonly writes an `IRatingStrategy` interface and a single
`StandardRatingStrategy` behind it, because that is the shape good C# code
for "a pluggable calculation" looks like regardless of how many calculations
currently exist. The resulting code reviews cleanly on its own terms:
small interface, one focused implementation, a caller that depends on the
abstraction. What a reviewer has to catch, and what the analyzer in this
chapter catches instead, is that the second case the interface exists to
support, express service, has not been written yet, and the review has no
way to tell whether it is coming next week or never.

## SHIP0002: a compilation-wide count behind a build warning

`SingleImplementationStrategyAnalyzer` lives in `RatingReview.Analysis`, a
project whose only job is to compile into a Roslyn diagnostic analyzer and
run as a plugin inside every build of the consuming library. It declares one
diagnostic, `SHIP0002`, in the `ShippingReview` category at
`DiagnosticSeverity.Warning`:

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

The question this diagnostic answers, "how many classes implement this
interface," cannot be answered by looking at any single type in isolation.
A symbol callback that only ever sees one class at a time has no way to know
whether a second implementation sits in another file the compiler has not
visited yet, or never visits at all. The analyzer solves that by splitting
the work across two registrations inside one `CompilationStartAction`: a
`SymbolAction` that fires once per named type across the whole compilation,
and a `CompilationEndAction` that runs only after every type has been seen.

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

`CollectType` runs once per type symbol and does one of two things: if the
type itself is a candidate Strategy interface, it records the interface; if
the type is a class or struct, it walks `AllInterfaces` looking for any
candidate Strategy interface among them and adds itself to that interface's
list of implementers. Both dictionaries are keyed by `ISymbol` with
`SymbolEqualityComparer.Default`, because Roslyn symbol analysis runs
concurrently across the compilation by default, and a plain
`Dictionary<TKey, TValue>` is not safe under that concurrency.
`ReportSingleImplementations` then runs exactly once, after every type in
the compilation has already passed through `CollectType`, and reports
`SHIP0002` for every candidate interface whose implementer count is exactly
one. `RS1037` is the reason `customTags: WellKnownDiagnosticTags.CompilationEnd`
appears on the descriptor itself: a diagnostic that can only be judged once
the whole compilation has been seen has to say so, and the Roslyn analyzer
rules enforce that the tag and the registration agree.

The naming-scope guard keeps the analyzer from reaching past the one shape
it is built to flag:

```csharp
private static bool IsStrategyInterface(INamedTypeSymbol type) =>
    type.TypeKind == TypeKind.Interface && type.Name.EndsWith(StrategySuffix, StringComparison.Ordinal);
```

An interface named `ISlotAllocator` with one implementation is not a
candidate at all, regardless of how many or how few classes implement it;
the analyzer only ever opens a question about interfaces whose name already
declares, by the project's own convention, that they are meant to hold
interchangeable strategies. That scope is deliberate. A single-implementation
interface is not inherently wrong; narrowing the rule to the one naming
convention this project uses for truly pluggable behavior keeps the
diagnostic from firing on every small interface a reviewer extracted for
perfectly good reasons unrelated to the Strategy pattern.

Reporting lands at the interface's own identifier, not at the
implementation or at some synthetic location:

```csharp
var location = interfaceSymbol.Locations.FirstOrDefault(static location => location.IsInSource)
    ?? Location.None;

context.ReportDiagnostic(Diagnostic.Create(Rule, location, interfaceSymbol.Name, implementation.Name));
```

Placing the squiggle on `IRatingStrategy` itself, rather than on
`StandardRatingStrategy`, matches where the actual decision lives: whether
this interface is worth keeping is a question about the abstraction, not
about any one class behind it. The message format carries both names,
`interfaceSymbol.Name` and `implementation.Name`, so a developer reading the
warning in an editor does not need to go find the sole implementation
separately; the diagnostic already says which one it is.

## The corrected sample: two strategies, nothing to flag

`RatingReview.csproj`, the top-level library this analyzer protects, hosts
the reviewed form of the same feature: `IRatingStrategy` with two concrete
implementations, each pricing a `BookingRequest` under a different service
level.

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

`StandardRatingStrategy` charges a flat per-kilogram rate plus a modest
insurance surcharge on the declared value, the ground-service formula the
first draft already had. `ExpressRatingStrategy` adds a real second shape:
a higher per-kilogram rate for the faster transit, a fixed handling fee that
ground service never charges, and a steeper insurance rate reflecting the
higher-value freight express service tends to carry. These are not two
classes wired up to satisfy an analyzer; they are two measurably different
pricing formulas a shipping system has to support, which is exactly the
case `IRatingStrategy` was named to cover from the start. Once a second,
distinct shape exists behind the interface, `SingleImplementationStrategyAnalyzer`
has nothing to report. The compilation it walks finds two implementers for
`IRatingStrategy`, the implementer count check in `ReportSingleImplementations`
fails to match, and no diagnostic fires. The analyzer never objects to the
interface itself, only to the gap between what an interface promises and
how many shapes currently back that promise; close the gap with a
formula a real booking needs rather than a placeholder written to satisfy
the build, and the warning disappears because the condition it was
checking for is no longer true.

That last distinction matters more than the mechanics of the analyzer
itself. `SHIP0002` cannot tell a real, distinct second strategy from a second class
added purely to quiet the warning, one that delegates back to the first
strategy's formula or hardcodes a value nobody asked for. Making the
diagnostic go away is a few minutes of work regardless of which path a
developer takes. The analyzer's contribution is forcing that choice into the
open at build time, not making it for anyone. A human still decides whether
the right answer is "add `ExpressRatingStrategy` because express service is
a real requirement" or "collapse `IRatingStrategy` back into a single
`RatingCalculator` class because no second service level is
planned." Both are legitimate fixes. Only one of them is the right fix for
any given booking system, and the analyzer's job ends at pointing out that a
decision is owed, not at making it.

## Testing the analyzer with the raw Roslyn compiler API

`RatingReview.Tests` drives the analyzer through `AnalyzerTestHarness`
rather than a packaged analyzer-testing library. The harness compiles a
source string directly with the compiler APIs a Roslyn analyzer already
runs inside of: `CSharpSyntaxTree.ParseText`, `CSharpCompilation.Create`,
and `WithAnalyzers` followed by `GetAnalyzerDiagnosticsAsync`.

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

Reference assemblies come from the running .NET host's
trusted-platform-assembly list rather than a reference-assembly NuGet
package, which keeps each test snippet compiling against the same runtime
that is already hosting the test process, with nothing more to restore.
Each snippet passed into the harness is a complete, self-contained
compilation unit, a whole `namespace RatingReview { ... }` block with its
own interface and implementation, so no test depends on the real
`RatingReview` assembly or risks one snippet's types leaking into another's
compilation.

`SingleImplementationStrategyAnalyzerTests` runs seven cases against that
harness. `Fires_on_a_strategy_interface_with_one_implementation` confirms
the positive case: an `ISurchargeStrategy` interface with one
`FuelSurchargeStrategy` behind it produces exactly one diagnostic.
`Quiet_on_a_strategy_interface_with_two_implementations` proves the
negative that matters most, that a real second shape silences the warning,
using an `IDemurrageStrategy` interface with a flat and a tiered
implementation. `Quiet_on_a_single_implementation_interface_not_named_strategy`
checks the naming-scope guard directly: an `ISlotAllocator` interface with
one implementation produces nothing, because the interface's name never put
it in scope for this rule in the first place.
`Quiet_when_a_strategy_interface_has_no_implementations` confirms the
analyzer does not misfire on an interface that nothing implements yet, a
shape that is not the smell this rule targets. Two more tests pin down the
diagnostic's shape rather than just whether it fires:
`Reports_correct_id_severity_and_location` asserts the diagnostic carries id
`SHIP0002`, severity `Warning`, and a source location whose exact text is
the interface's own identifier, `IRoutingStrategy`, and
`Message_names_the_interface_and_its_sole_implementation` asserts the
message text contains both `IConsolidationStrategy` and
`VolumeConsolidationStrategy`. The seventh,
`Flags_each_single_implementation_strategy_interface_independently`, puts
two unrelated single-implementation Strategy interfaces in one compilation
and asserts both are flagged, proving the per-interface bookkeeping in
`implementersByInterface` keeps separate interfaces from interfering with
each other's counts.

Driving the analyzer through the compiler's own APIs, rather than through a
dedicated analyzer-testing package, keeps every test grounded in what the
analyzer does inside a real build: parse, compile, run analyzers
against the finished compilation, and inspect exactly the diagnostics that
come back. Nothing about the test setup is specific to a testing framework's
notion of what an analyzer test should look like; it is the same sequence
`dotnet build` runs, called directly.

## Reviewing AI output as a human-owned gate

None of this makes reviewing an assistant's code automatic. `SHIP0002`
answers one narrow structural question, how many classes implement an
interface named for a Strategy, and it answers that question the same way
for code a person wrote by hand and code an assistant drafted in seconds.
What it changes is where the review rule lives. Written down as a sentence
in a style guide, "don't introduce a Strategy interface until you have two
real implementations" is a rule a reviewer has to remember, apply
consistently, and re-derive by eye on every diff, exactly the kind of rule
that degrades under fatigue, deadline pressure, or simple unfamiliarity with
a new teammate's code. Written down as an analyzer, the same rule runs on
every compilation, flags the same shape every time regardless of who wrote
it or how persuasive the surrounding code reads, and shows up as a warning
in the build output a developer cannot scroll past without noticing.

That shift, from a rule someone has to remember to a rule the build
enforces, is the right response to how quickly an assistant can produce
plausible-looking abstractions. A single-implementation Strategy interface
is not a bug. It compiles, it passes whatever tests already exist for the
one implementation behind it, and it often reads as careful design to a
reviewer skimming a large diff quickly. An analyzer that encodes exactly
this shape as a build-time check does not need a reviewer to catch it on
sight; it turns a judgment call that used to depend entirely on attention
into a flag that appears whether or not the reviewer happened to look
closely at that one file. The analyzer technique this book has used
elsewhere to encode a design rule as a compiler diagnostic applies here with
no changes beyond the shape being checked: name the smell precisely enough
that the analyzer only fires on it, report at the location a developer
needs to look, and let the build itself be the thing that never
forgets to ask the question.

The decision that follows a `SHIP0002` warning still belongs to a person,
and that division of labor is the actual point of building the analyzer in
the first place. The analyzer cannot know whether express service is a real
requirement landing next sprint or a feature nobody has asked for yet; it
can only count implementers and report when the count is one. A developer
reading the warning has context the analyzer never will: the ticket that
asked for this feature, what pricing rules are planned next, and
the judgment to tell a real, distinct second strategy from a second class written
purely to quiet a warning. Catching the shape is mechanical and belongs to
the tool. Deciding what to do about it, collapse the abstraction or build
the second strategy in good faith, stays a human call every time, and no
analyzer, however precisely it is written, changes which side of that line
it sits on.

## Build, run, observe

The example behind this chapter is
`examples/46-working-with-ai-and-llm-tools`:
`RatingReview.Analysis` hosting `SingleImplementationStrategyAnalyzer`,
the corrected `RatingReview` consumer with `IRatingStrategy`'s two distinct
implementations, and `RatingReview.Tests` driving the analyzer through
`AnalyzerTestHarness`. It is standalone-testable from its own directory:

```sh
cd examples/46-working-with-ai-and-llm-tools
dotnet test
```

Observed locally with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 7
  failed: 0
  succeeded: 7
  skipped: 0
```

`dotnet build` is also green with zero warnings and zero errors under this
project's warnings-as-errors setting, because `RatingReview`'s corrected
`IRatingStrategy` has two real implementations and never trips `SHIP0002`
in its own compilation. The seven test cases split across the single
`SingleImplementationStrategyAnalyzerTests` class: four cases establish
when the diagnostic does and does not fire (one implementation, two
implementations, a non-`Strategy` name, zero implementations), one checks
the diagnostic's id, severity, and reported location, one checks that the
message names both the interface and its sole implementation, and one
confirms two unrelated single-implementation interfaces in the same
compilation are each flagged independently.

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
- Microsoft Learn, [How to write a Roslyn analyzer](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/analyzers-overview): the `DiagnosticAnalyzer` and `DiagnosticDescriptor` shapes, symbol actions, and compilation-wide analysis this chapter's analyzer follows.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
