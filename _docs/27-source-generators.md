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

- Feed a build-time input to an `IIncrementalGenerator` through `AdditionalFiles` and `AdditionalTextsProvider`.
- Build the parse, combine and emit pipeline, and name each stage for tracking.
- Make the pipeline's model compare by value with `EquatableArray<T>`, so an unchanged manifest is cached.
- Emit adapter classes and a registry that hand-written code calls like any other type.
- Say why a generated registry survives trimming and native AOT where a reflection scan doesn't.

## Why this matters

A registry mapping a carrier code to its gateway needs one entry per carrier. The runtime way is a startup scan: reflect over loaded assemblies, find every `ICarrierGateway`, instantiate each with `Activator.CreateInstance`. Adding a carrier never touches the registry, but the contents are invisible until the scan runs, and every type it might find has to stay reachable through reflection.

A source generator builds the same registry at compile time. It runs inside the compiler, reads the project's input and adds ordinary C# to the compilation. The generated `<Code>CarrierGateway` classes are compiled types like any other, and the trimmer and the native AOT compiler see them as referenced code. Adding a carrier becomes a one-line manifest change.

{% include excalidraw.html name="27-source-generators" caption="Figure 27.1: CarrierGatewayGenerator reads carriers.manifest at build time and emits one adapter class per row plus a registry exposing lookup by code. CarrierDispatch calls through that generated registry alone, never naming a generated type directly." %}

## A manifest as the generator's input

`carriers.manifest` lists one carrier per line: code, display name, endpoint.

```
DHLX,DHL Express,https://api.dhlx.example
FDXG,FedEx Ground,https://api.fdxg.example
UPSX,UPS Worldwide,https://api.upsx.example
USPS,USPS Priority,https://api.usps.example
```

The consuming project declares it as an additional file, not a compile item:

```csharp
<ItemGroup>
    <AdditionalFiles Include="carriers.manifest" />
</ItemGroup>
```

The compiler doesn't parse `AdditionalFiles` as C#, but anything running inside the compilation can read them, which suits a manifest, a config file or a schema. The same project references the generator with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`: it loads as a compiler extension, and there's nothing in it to link against.

The contract every adapter implements is hand-written:

```csharp
public interface ICarrierGateway
{
    string Code { get; }

    string DisplayName { get; }

    string Endpoint { get; }
}
```

A person decides the shape each gateway honors. The generator decides how many exist and what each returns.

## The incremental pipeline

`Initialize` generates nothing itself. It wires a pipeline of provider stages:

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

| Stage | Input | Output |
| --- | --- | --- |
| `.Where` | every `AdditionalText` | files ending `.manifest` |
| `.Select(Parse)`, tracked as `CarrierManifest.Parse` | one file's text | a `CarrierManifestModel` for that file |
| `.Collect().Select(Combine)`, tracked as `CarrierManifest.Combine` | every parsed model | one merged model |
| `RegisterSourceOutput(model, Emit)` | the merged model | `GeneratedCarrierRegistry.g.cs` via `AddSource` |

The skeleton isn't specific to carriers. Any build-time input a generator reads and turns into types follows the same filter, parse, combine and emit shape.

{% include excalidraw.html name="27-incremental-generator-pipeline" caption="Figure 27.2: The generator pipeline parses the manifest, combines every parsed file into one model, and emits source from it. Because that model compares by value, an unchanged manifest produces an unchanged model and the pipeline skips emitting entirely." %}

## The caching contract: why EquatableArray&lt;T&gt;

Each stage caches its output and compares the new value with the previous run's using `Equals`. If they're equal, the compiler reuses the cached result and skips every stage downstream. So the model must compare by value:

```csharp
internal sealed record CarrierEntry(string Code, string DisplayName, string Endpoint);

internal readonly record struct CarrierManifestModel(EquatableArray<CarrierEntry> Entries);
```

`CarrierEntry` is a record, so equal strings mean equal entries. The trap is the collection. `ImmutableArray<T>` equality compares the backing array instance, not the elements. Reparsing an unchanged manifest builds a new array with the same four rows, so a model holding a plain `ImmutableArray<CarrierEntry>` would look changed on every run, and `Combine` and `Emit` would rerun every time. `EquatableArray<T>` compares contents:

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

The record struct's generated equality delegates to `EquatableArray<T>.Equals`, so two parses of the same manifest give equal models. The `IEquatable<T>` constraint is satisfied by any record element.

Cached output and correct output are separate properties, so check the driver's own bookkeeping. Run the generator with `trackIncrementalGeneratorSteps: true` and read each tracked step's `IncrementalStepRunReason`. Both steps report `New` on the first run. After that, two cases differ:

| Second run | `Parse` | `Combine` |
| --- | --- | --- |
| Another file changed, same manifest text object | `Cached` | `Cached` |
| Manifest re-read, same content, `EquatableArray<CarrierEntry>` | `Unchanged` | `Cached` |
| Manifest re-read, same content, plain `ImmutableArray<CarrierEntry>` | `Modified` | `Modified` |

In the first case `Parse` never reruns, so the model's equality doesn't matter. Value equality matters in the second: `Parse` reruns, produces an equal model, and everything downstream is skipped.

## What gets emitted, and who calls it

`Emit` writes one class per row and a registry:

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

Four rows become `DHLXCarrierGateway`, `FDXGCarrierGateway`, `UPSXCarrierGateway` and `USPSCarrierGateway`, each returning string literals. `GeneratedCarrierRegistry.All` holds one instance of each, and a `Find` method, appended the same way, looks one up by code. An empty model emits nothing: a guard returns early, so no empty registry ships.

Hand-written code calls the registry and never names a generated class:

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

`GeneratedCarrierRegistry.Find` is an ordinary static call against a type that exists in no source file until the generator runs.

## The trimming and AOT argument

{% include excalidraw.html name="27-generated-registry-vs-reflection" caption="Figure 27.3: A generated registry references each adapter statically at compile time, so trimming and native AOT see ordinary code. The reflection-based alternative it replaces scans loaded assemblies at startup, a path trimming can remove types out from under." %}

| | Reflection scan at startup | Generated registry |
| --- | --- | --- |
| How types are found | `Assembly.GetTypes()` and `Activator.CreateInstance` | `new` expressions in `All`'s initializer |
| Trimmer sees the adapters | no; they need `DynamicallyAccessedMembers` or a root descriptor | yes, as referenced code |
| Native AOT | membership decided at run time, which AOT must know ahead | every type known at compile time |
| Contents visible | after the scan runs | in the generated source |

A trimmer removes code nothing visibly calls, and a type reached only by an assembly scan is invisible to that analysis. The generated registry never depends on reflection, so a trimmed or AOT build needs no annotation to keep the four adapters.

## When not to use it

- **A small, fixed set.** Four gateway classes for four carriers that never change are easier to read and step through than a generator, a manifest and the wiring between them. A `static readonly` list does the job.
- **A team new to generator debugging.** The generator runs in the compiler process, so breakpoints in application code never stop inside `Emit`. You inspect what it produced by reading the emitted source, for example `runResult.GeneratedTrees` from a `CSharpGeneratorDriver` run.
- **Without a caching check.** A model holding a plain `ImmutableArray<CarrierEntry>` still compiles and emits correct adapters. The only symptom is every stage rerunning whenever the manifest is re-read. Track the pipeline steps across a re-read with the same content and assert `Unchanged` or `Cached`.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a source generator.** "Add a source generator for the carrier registry" doesn't say which generator interface to use or how the pipeline's model compares. State it:

> In `CarrierGatewayGenerator`, implement `IIncrementalGenerator`, not `ISourceGenerator`, which reruns all of `Execute` on every compilation. Read `carriers.manifest` through `AdditionalTextsProvider`, filtered on the `.manifest` extension, and name the parse and combine stages with `WithTrackingName` using `TrackingNames.Parse` and `TrackingNames.Combine`. Hold the entries of `CarrierManifestModel` in an `EquatableArray<CarrierEntry>` so the model compares by value. Emit one `ICarrierGateway` per entry plus `GeneratedCarrierRegistry`.

Run `dotnet test --project CarrierManifest.Tests --filter GeneratorCachingTests`; `Equal_content_manifest_reread_leaves_the_model_unchanged` is the test that catches a model holding a plain `ImmutableArray<T>`.
</blockquote>

## Build, run, observe

```sh
cd examples/27-source-generators && dotnet test
```

All 6 tests pass. They drive the generator with `CSharpGeneratorDriver` against an in-memory manifest and read the generated trees off the run result. `dotnet build` runs the generator as part of ordinary compilation, compiling the adapters and registry into `CarrierManifest.dll`; the generated file is written to disk only if the project sets `EmitCompilerGeneratedFiles`. One experiment:

- **Hold a plain array.** Change `CarrierManifestModel` to wrap `ImmutableArray<CarrierEntry>`. The emit tests and the unrelated-change caching test still pass. The re-read test fails: after `driver.ReplaceAdditionalText` swaps in an equal-content manifest, `Parse` reports `Modified` instead of `Unchanged`.

## For deeper coverage

- Microsoft Learn, [Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview): how a generator runs inside the compiler and adds source to the build.
- Microsoft Learn, [Incremental generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/incremental-generators): `IIncrementalGenerator`, the provider pipeline, and the value-equality contract behind caching.
- Microsoft Learn, [AdditionalText Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.additionaltext): the `AdditionalFiles` input and `AdditionalTextsProvider`.
- The .NET Compiler Platform SDK, [Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md): worked patterns for incremental pipelines, including the equatable-model shape.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
