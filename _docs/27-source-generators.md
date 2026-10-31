---
title: "Source generators: patterns at build time"
order: 27
layout: tutorial
sectionid: docs
part: "idiomatic-csharp"
description: "Replacing reflection-based factories, registries, and mappers at build time, and the AOT and trimming argument."
---

<!-- EXAMPLE (shipping domain): Generating carrier gateway adapters from a manifest at build time. -->

## Learning objectives

By the end of this chapter you can:

- Explain how `CarrierGatewayGenerator`, an `IIncrementalGenerator`, reads
  `carriers.manifest` through `context.AdditionalTextsProvider`, and why a
  source generator runs inside the host compiler process rather than
  alongside the application it contributes to.
- Trace the pipeline `CarrierGatewayGenerator.Initialize` builds, `Parse` on
  each `AdditionalText`, `Collect().Select(Combine)` across every matching
  file, and `RegisterSourceOutput(model, Emit)`, and say what
  `CarrierManifestModel` each stage hands to the next.
- Explain why wrapping `CarrierManifestModel`'s entries in
  `EquatableArray<CarrierEntry>` instead of a plain
  `ImmutableArray<CarrierEntry>` is what lets the incremental pipeline cache
  an unchanged manifest, and how `.WithTrackingName` plus
  `IncrementalStepRunReason` prove that caching holds.
- Read the adapters `CarrierGatewayGenerator` emits per manifest row, the
  `<Code>CarrierGateway` classes and the `GeneratedCarrierRegistry` that
  lists and looks them up, and explain how `CarrierDispatch.ResolveEndpoint`
  calls through to generated types it never names directly.
- Say positively why a compile-time registry survives trimming and native
  ahead-of-time compilation in a way a runtime scan over loaded assemblies
  would not.

## Why this matters

A registry that maps a carrier code to the gateway responsible for it needs
an entry for every carrier a shipping system supports. One way to build that
registry is to scan for it at startup: reflect over every loaded assembly,
find every type implementing `ICarrierGateway`, instantiate one of each, and
keep the result in a dictionary for the life of the process. That approach
works, and it means adding a carrier never touches the registry's own code.
It also means the registry's contents are invisible until the scan runs, and
every type the scan might find has to stay reachable through reflection for
the scan to find it.

.NET 10 and C# 14 support building that same registry at build time instead,
through `Microsoft.CodeAnalysis`'s `IIncrementalGenerator` interface. A
source generator runs inside the compiler itself, reads whatever input the
project gives it, and emits ordinary C# source that becomes part of the
compilation it ran inside. Nothing about the generated code is special at
runtime. A `<Code>CarrierGateway` class produced this way is a compiled type
exactly like one a person typed by hand, callable, debuggable, and, this is
the payoff a reflection-based scan cannot offer, visible to the trimmer and
to native ahead-of-time compilation as ordinary referenced code rather than
as a reflection target the tooling has to be told to preserve. Code the
compiler can see because something in the project calls it directly survives
trimming automatically. Code a runtime scan would otherwise have discovered
by walking loaded assemblies does not get that guarantee without extra work,
and keeping it reachable usually means annotating it, rooting it, or
disabling trimming for the assembly that holds it.

This chapter builds that registry the generated way. A plain text manifest
lists each carrier once; a generator reads the manifest at compile time and
emits one adapter class per row plus the registry that exposes all of them.
Adding a carrier becomes a one-line change to the manifest. No hand-written
class, no runtime scan, and no reflection ever runs to discover what the
registry contains, because the registry's contents were already decided
before the program started.

{% include excalidraw.html name="27-source-generators" caption="Figure 27.1: carriers.manifest lists the DHLX, FDXG, UPSX, and USPS carrier rows as an AdditionalFiles input. CarrierGatewayGenerator, an IIncrementalGenerator, reads that manifest at build time and emits one adapter per row, such as DHLXCarrierGateway, plus a GeneratedCarrierRegistry exposing All and Find(code). CarrierDispatch calls through the registry alone, never naming a generated type directly." %}

## A manifest as the generator's input

The example behind this chapter lives in `examples/27-source-generators`.
`carriers.manifest` is a plain text file, one carrier per line, each line a
code, a display name, and an endpoint separated by commas:

```
DHLX,DHL Express,https://api.dhlx.example
FDXG,FedEx Ground,https://api.fdxg.example
UPSX,UPS Worldwide,https://api.upsx.example
USPS,USPS Priority,https://api.usps.example
```

`CarrierManifest.csproj`, the project the generator contributes to, lists
this file as an `AdditionalFiles` item rather than a compile item:

```csharp
<ItemGroup>
    <AdditionalFiles Include="carriers.manifest" />
</ItemGroup>
```

`AdditionalFiles` is the MSBuild item group a generator reads through
`AdditionalTextsProvider`; it carries files the compiler never parses as
C# source but still makes visible to anything running inside the
compilation, exactly the shape a manifest, a config file, or a schema needs.
The same project also references the generator itself with
`OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`, which
loads the generator assembly as a compiler extension rather than as a normal
library dependency; the generator's own output is C# source text, not a type
the consuming project calls into directly, so there is nothing in it for the
consuming project to link against.

The contract every generated adapter implements is hand-written and ordinary,
not something the generator defines or could change:

```csharp
public interface ICarrierGateway
{
    string Code { get; }

    string DisplayName { get; }

    string Endpoint { get; }
}
```

The generator targets this interface by name for each manifest row. Keeping
the contract hand-written and the rows generated is the split this chapter
is built around: a person decides the shape every carrier gateway has to
honor, and the generator decides how many gateways exist and what each one's
three properties return, reading that decision straight out of a text file
instead of out of a person's typed-out class bodies.

## The incremental pipeline from file to source

`CarrierGatewayGenerator` implements `IIncrementalGenerator`, the interface
every current .NET source generator is written against. Its single method,
`Initialize`, does not generate anything itself. It wires up a pipeline, a
sequence of provider stages, each one taking the previous stage's output and
producing the next:

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    var parsedManifests = context.AdditionalTextsProvider
        .Where(static file => file.Path.EndsWith(ManifestExtension, StringComparison.OrdinalIgnoreCase))
        .Select(static (text, cancellationToken) => Parse(text, cancellationToken))
        .WithTrackingName(TrackingNames.Parse);

    var model = parsedManifests.Collect()
        .Select(static (manifests, _) => Combine(manifests))
        .WithTrackingName(TrackingNames.Combine);

    context.RegisterSourceOutput(model, Emit);
}
```

`context.AdditionalTextsProvider` surfaces every `AdditionalFiles` entry the
project declares, as a stream of `AdditionalText` values the pipeline can
filter and transform. `.Where` keeps only files ending in `.manifest`, so
the generator never tries to parse some other additional file the project
happens to carry. `.Select` then calls `Parse` on each surviving file,
turning raw text into a `CarrierManifestModel`, a small record struct
wrapping the carrier rows that file contained. `.WithTrackingName` gives
that stage a name, `"CarrierManifest.Parse"`, that shows up later when
something inspects how the pipeline behaved across two runs, which is
exactly what this chapter's caching tests do.

`.Collect()` gathers every parsed manifest into one `ImmutableArray`, because
a project could in principle declare more than one `.manifest` file, and
`.Select(Combine)` merges all of them into a single `CarrierManifestModel`
holding every carrier row from every manifest found. That merged model gets
its own tracking name, `"CarrierManifest.Combine"`. The pipeline's last step,
`context.RegisterSourceOutput(model, Emit)`, tells the compiler to call
`Emit` whenever the combined model is available, handing it a
`SourceProductionContext` it uses to add the generated file to the
compilation with `context.AddSource`.

Each stage only does one job. `Parse` turns one file's text into entries.
`Combine` turns however many parsed files exist into one model. `Emit` turns
one model into source text. None of the three stages know about the others'
internals, and nothing about this shape is specific to carrier gateways;
the same `AdditionalTextsProvider` → parse → combine → `RegisterSourceOutput`
skeleton applies to any build-time input a generator reads once and converts
into compiled types.

{% include excalidraw.html name="27-incremental-generator-pipeline" caption="Figure 27.2: AdditionalTextsProvider surfaces carriers.manifest, Parse turns it into a CarrierManifestModel wrapping an EquatableArray<CarrierEntry>, and Collect().Select(Combine) merges every parsed manifest into one model before RegisterSourceOutput(model, Emit) runs. Because CarrierManifestModel compares by value, an unchanged manifest produces an unchanged model and the pipeline skips Emit entirely." %}

## The caching contract: why EquatableArray&lt;T&gt;

An incremental generator earns its name by skipping work a plain generator
would redo on every keystroke. Every provider stage in the pipeline above
caches its output by comparing it to what the same stage produced on the
previous run; if the new value is equal to the old one, the compiler reuses
the cached result and does not call the stage's delegate again, let alone
every stage downstream of it. That comparison runs through ordinary
`Equals`, which is the entire reason `CarrierManifestModel` is written the
way it is:

```csharp
internal sealed record CarrierEntry(string Code, string DisplayName, string Endpoint);

internal readonly record struct CarrierManifestModel(EquatableArray<CarrierEntry> Entries);
```

`CarrierEntry` is a record, so two entries with the same three strings are
already equal to each other. The problem sits one level up, in how the
model holds its collection of entries. `ImmutableArray<T>` only ever
compares equal to the exact same backing array instance; two separately
built `ImmutableArray<CarrierEntry>` values holding the identical four rows,
in the identical order, are not equal to each other by `ImmutableArray<T>`'s
own `Equals`, because that comparison checks array identity, not element
content. If `CarrierManifestModel` wrapped a plain
`ImmutableArray<CarrierEntry>`, reparsing an unchanged `carriers.manifest`
on a second build would produce a new array with the same four rows, and the
pipeline would see that as a changed model every time, because the arrays
themselves are two different objects. Every downstream stage, `Combine` and
`Emit` both, would rerun on every build regardless of whether the manifest's
contents moved at all.

`EquatableArray<T>` exists to fix exactly that mismatch:

```csharp
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _values;

    public EquatableArray(ImmutableArray<T> values)
    {
        _values = values;
    }

    private ImmutableArray<T> ValuesOrEmpty => _values.IsDefault ? ImmutableArray<T>.Empty : _values;

    public bool Equals(EquatableArray<T> other) => ValuesOrEmpty.SequenceEqual(other.ValuesOrEmpty);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var value in ValuesOrEmpty)
        {
            hash = (hash * 31) + value.GetHashCode();
        }

        return hash;
    }
}
```

`EquatableArray<T>`'s `Equals` calls `SequenceEqual` across the wrapped
arrays instead of comparing array identity, so two instances holding the
same elements in the same order are equal regardless of which array object
backs either one. Because `CarrierManifestModel` is a `record struct`
wrapping an `EquatableArray<CarrierEntry>`, its own generated equality
delegates straight to `EquatableArray<T>.Equals`, and two parses of an
unchanged manifest now produce models the pipeline recognizes as identical.
The `where T : IEquatable<T>` constraint is what makes this work for any
element type the pipeline might wrap, not just `CarrierEntry`: a record
already implements `IEquatable<T>` of itself, which is why `CarrierEntry`
needed no extra code to satisfy the constraint.

`GeneratorCachingTests.Incremental_pipeline_caches_when_the_manifest_is_unchanged`
proves the fix holds, not by checking that the generated source looks the
same twice, but by reading the driver's own bookkeeping. It runs
`CarrierGatewayGenerator` with `trackIncrementalGeneratorSteps: true`,
drives it once and asserts every tracked step under the names
`"CarrierManifest.Parse"` and `"CarrierManifest.Combine"` reports
`IncrementalStepRunReason.New`, then drives it a second time against a
separately built but content-equivalent compilation and asserts the same
steps now report `IncrementalStepRunReason.Cached` or `.Unchanged`. That
second run stands in for the ordinary case an incremental generator has to
stay cheap for: something else in the project changed and triggered a
rebuild, the manifest itself did not, and the pipeline's own tracked steps
show that `Parse` and `Combine` were skipped rather than repeated.

## What gets emitted, and who calls it

`Emit`, the delegate `RegisterSourceOutput` calls once the combined model is
ready, builds one `<Code>CarrierGateway` class per entry and a single
registry that lists all of them:

{% raw %}
```csharp
foreach (var entry in model.Entries)
{
    builder.AppendLine($"public sealed class {entry.Code}CarrierGateway : ICarrierGateway");
    builder.AppendLine("{");
    builder.AppendLine($"    public string Code => \"{entry.Code}\";");
    builder.AppendLine();
    builder.AppendLine($"    public string DisplayName => \"{entry.DisplayName}\";");
    builder.AppendLine();
    builder.AppendLine($"    public string Endpoint => \"{entry.Endpoint}\";");
    builder.AppendLine("}");
    builder.AppendLine();
}

builder.AppendLine("public static class GeneratedCarrierRegistry");
builder.AppendLine("{");
builder.AppendLine("    public static IReadOnlyList<ICarrierGateway> All { get; } = new List<ICarrierGateway>");
builder.AppendLine("    {");
foreach (var entry in model.Entries)
{
    builder.AppendLine($"        new {entry.Code}CarrierGateway(),");
}

builder.AppendLine("    };");
```
{% endraw %}

That loop runs once per manifest row, so four rows become four classes,
`DHLXCarrierGateway`, `FDXGCarrierGateway`, `UPSXCarrierGateway`, and
`USPSCarrierGateway`, each one returning its code, display name, and
endpoint as a string literal baked directly into the property. The same
`Emit` method goes on to append a `GeneratedCarrierRegistry` class whose
`All` property lists one instance of each generated class (shown above) and
whose `Find` method, appended the same way with further `AppendLine` calls,
walks that list for a matching code. `context.AddSource` hands the finished
text to the compiler under a name of the generator's own choosing,
`GeneratedCarrierRegistry.g.cs`, and from that point on the compiler treats
the text exactly as it would a file a person wrote: parsed, type-checked,
and compiled into the same assembly as everything else in the project.

`CarrierDispatch`, the hand-written class that resolves a carrier code to
its endpoint, never names a single generated class:

```csharp
public sealed class CarrierDispatch
{
    private readonly List<string> _dispatchLog = [];

    public string ResolveEndpoint(string code)
    {
        var gateway = GeneratedCarrierRegistry.Find(code);
        if (gateway is null)
        {
            throw new InvalidOperationException($"No carrier gateway registered for code '{code}'.");
        }

        _dispatchLog.Add($"{gateway.Code} -> {gateway.Endpoint}");
        return gateway.Endpoint;
    }
}
```

`ResolveEndpoint` calls `GeneratedCarrierRegistry.Find`, an ordinary static
method call against a type that did not exist in any source file until the
generator ran. Nothing distinguishes that call from a call against a
hand-written type anywhere else in the project; the only sign the type is
generated is the one-line manifest edit it would take to add a fifth
carrier. `GeneratorEmitTests.Generated_code_compiles_with_no_errors` checks
that this whole arrangement compiles, driving the generator
directly against an in-memory `AdditionalText` and an in-memory copy of
`ICarrierGateway` with `CSharpGeneratorDriver`, then asserting the updated
compilation has no error diagnostics. `Generator_produces_no_adapter_output_without_a_manifest`
checks the other edge: with no `AdditionalText` supplied at all,
`RegisterSourceOutput` still runs, `Emit` sees an empty model, and the
`if (model.Entries.Length == 0) return;` guard means no file is added and no
empty `GeneratedCarrierRegistry` class ships with nothing in it.

## The trimming and AOT argument

A reflection-based version of this same registry would typically build
itself once, at process startup, by enumerating loaded assemblies, finding
every type implementing `ICarrierGateway`, and instantiating one of each
with `Activator.CreateInstance`. That code is straightforward to write and
expensive to trust under a trimmed or natively compiled build. A trimmer
removes code nothing visibly calls; a type that is only ever reached by a
runtime scan over `Assembly.GetTypes()` is invisible to that analysis unless
something tells the trimmer to keep it anyway, typically a
`DynamicallyAccessedMembers` annotation or a root descriptor naming the type
by hand. Native ahead-of-time compilation is stricter still: it needs to
know every type and member a program might reach before it runs, and a
registry whose membership is only decided at runtime by scanning assemblies
is exactly the shape that compilation model struggles with.

`GeneratedCarrierRegistry` sidesteps the problem instead of solving it.
`CarrierDispatch.ResolveEndpoint` calls `GeneratedCarrierRegistry.Find`
directly, `Find` constructs each `<Code>CarrierGateway` with an ordinary
`new` expression inside `All`'s initializer, and every one of those
constructor calls is visible to the compiler, the trimmer, and the
ahead-of-time compiler as ordinary referenced code, the same as any other
`new SomeType()` anywhere in the project. No reflection runs to discover
which types exist, because the generator already decided that answer at
build time and wrote it down as a list of `new` expressions. A trimmed or
natively compiled build of this project needs no root descriptor and no
`DynamicallyAccessedMembers` annotation to keep the four adapter classes
reachable, because they were never reachable only through reflection in the
first place.

## Build, run, observe

The example is standalone-testable from its own directory:

```sh
cd examples/27-source-generators && dotnet test
```

Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 5
  failed: 0
  succeeded: 5
  skipped: 0
  duration: 1s 690ms
```

The five cases split across two files. `GeneratorEmitTests` drives
`CarrierGatewayGenerator` directly with a raw `CSharpGeneratorDriver`
against an in-memory `AdditionalText`, proving the generator emits an
adapter class for each of the four manifest rows, that
`GeneratedCarrierRegistry` wires all four into `All` and answers `Find`
correctly, that the generated code compiles against the hand-written
`ICarrierGateway` contract with no diagnostics, and that supplying no
manifest at all produces no generated output rather than a
`GeneratedCarrierRegistry` with an empty `All` list. `GeneratorCachingTests`
drives the same generator twice with
`trackIncrementalGeneratorSteps: true` and asserts the `Parse` and
`Combine` pipeline steps move from `IncrementalStepRunReason.New` on the
first run to `.Cached` or `.Unchanged` on the second, the behavior
`EquatableArray<CarrierEntry>` exists to guarantee.

`dotnet build` from the same directory also runs the generator, as part of
ordinary compilation rather than as a separate step: `CarrierManifest.csproj`
references `CarrierManifest.Generation.csproj` as an analyzer, so every build
reads `carriers.manifest`, runs `CarrierGatewayGenerator`, and compiles the
resulting adapters and registry straight into `CarrierManifest.dll`. The
generated source is not written to a visible file during an ordinary build;
it exists only inside the compilation unless a project opts into
`EmitCompilerGeneratedFiles`. The tests above read it directly off the
`GeneratorDriver`'s run result instead, which is the same approach this
project's own CI takes to check the generator's output without needing that
extra opt-in.

## When not to use it

A source generator costs more than it pays when the set of types it would
produce is small and fixed. Four hand-written carrier gateway classes, typed
out once and never touched again because the company works with exactly
four carriers, are easier to read, step through, and change than a
generator, a manifest file, and the pipeline wiring that connects them. The
manifest-driven approach in this chapter pays off specifically because
carriers get added and removed over time and the registry has to track that
without a corresponding code change; a registry that never changes gets
nothing from being generated that a `static readonly` list would not provide
with far less machinery.

Debugging a generator is also a different exercise from debugging ordinary
code. `CarrierGatewayGenerator` itself runs inside the host compiler
process, not inside the application being built, so a breakpoint set in
application code never stops execution partway through `Emit`; finding out
what the generator produced for a given manifest means reading the emitted
source directly, the way this chapter's tests do by inspecting
`runResult.GeneratedTrees`, rather than stepping through the generator live
alongside the code it generates. A team unfamiliar with that split loses
real time the first time a generator misbehaves and the usual debugging
habits do not apply.

The caching contract behind the pipeline can also regress without raising
any error. A `CarrierManifestModel` built around a plain
`ImmutableArray<CarrierEntry>` instead of `EquatableArray<CarrierEntry>`
would still compile, still emit correct adapters, and still pass
`GeneratorEmitTests` without a single failing assertion; the only visible
symptom would be a pipeline that reruns every stage on every build, a cost
that shows up as slower builds rather than as a wrong answer. Writing the
caching test alongside the emit tests, the way `GeneratorCachingTests` does
here, is what catches a regression a correctness test alone would miss.

> **AI sidebar.** Asked to add a source generator for a registry like this
> one, an AI pairing partner will sometimes reach for `ISourceGenerator`, the
> interface source generators were originally shipped with, instead of
> `IIncrementalGenerator`. `ISourceGenerator` reruns its entire `Execute`
> method on every compilation, with no caching stage to skip unchanged
> input, so a generator written against it pays the full cost of reparsing
> `carriers.manifest` and re-emitting every adapter on every keystroke in an
> editor, not just on every build. A related suggestion builds the pipeline
> correctly with `IIncrementalGenerator` but wraps `CarrierEntry` values in a
> plain `ImmutableArray<T>` rather than `EquatableArray<T>`, which compiles
> and passes functional tests while losing the caching the interface exists
> to provide, with nothing in the output to flag the loss. Check a generated
> answer against what this chapter
> built: confirm the generator implements `IIncrementalGenerator`, confirm
> any collection crossing a pipeline stage boundary compares by value rather
> than by reference, and add a caching test with
> `trackIncrementalGeneratorSteps: true` rather than trusting that caching
> works because the generated output looks right.

## For deeper coverage

- Microsoft Learn, [Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview): the authority for how a source generator runs inside the compiler process and contributes compiled source to the build that hosts it.
- Microsoft Learn, [Incremental generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/incremental-generators): the authority for `IIncrementalGenerator`, the provider pipeline `CarrierGatewayGenerator.Initialize` builds, and the value-equality contract the caching behind `RegisterSourceOutput` depends on.
- Microsoft Learn, [AdditionalText Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.additionaltext): the authority for the `AdditionalFiles` input `carriers.manifest` arrives through and the `AdditionalTextsProvider` that surfaces it to a generator.
- The .NET Compiler Platform SDK, [Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md): worked patterns for incremental generator pipelines, including the equatable-model shape `CarrierManifestModel` and `EquatableArray<T>` follow in this chapter.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
