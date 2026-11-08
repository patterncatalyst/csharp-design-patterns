---
title: "Composite and Flyweight"
order: 20
layout: tutorial
sectionid: docs
part: "composing-adapting-structure"
description: "Composite over nested structure, and Flyweight through interning, FrozenDictionary, and pooling."
---

<!-- EXAMPLE (shipping domain): Consolidated shipments (a consignment of consignments); port and commodity codes interned. -->

## Learning objectives

By the end of this chapter you can:

- Give a `ShipmentParcel` leaf and a `Consolidation` branch one interface, `IConsignmentNode`, so a caller never branches on which it holds.
- Sum weight and flatten parcels recursively through a tree of any depth.
- Explain why `PortCode` and `CommodityCode` are plain sealed classes, not records, and check with `Assert.Same` that a registry hands back one shared instance per code.
- Seed a `FrozenDictionary` once with `ToFrozenDictionary`, and choose between a throwing `Get` and a non-throwing `TryGet`.
- Recognize when a tree adds machinery a flat list already covers, and when interning is wrong for a value that's nearly unique or varies per use.

## Why this matters

A bulk carrier groups parcels bound through the same corridor into a consolidation, and nests consolidations: depot parcels roll up into a regional consolidation, regional ones into a long-haul consolidation for one destination port, and that may be one line in a larger multimodal shipment. Anything asking how much a shipment weighs, or which parcels are inside it, needs an answer for a loose parcel and for the root of a three-level tree. Separate code per depth means every call site knows how deep its structure goes, and breaks when someone adds a level.

Inside that tree the same port and commodity codes repeat. Most volume moves through a handful of ports (Rotterdam, Los Angeles, Singapore, Hamburg, Shanghai), and a tree can hold thousands of parcels. A fresh `PortCode` per parcel, each with its own copy of "Rotterdam" and "NLRTM", repeats an allocation that should happen once per port. Harmonized System commodity codes are the same: heading 0901, coffee, appears on every shipment that carries coffee and never varies.

The tree is the Composite. The destination each leaf carries is the Flyweight.

{% include excalidraw.html name="20-composite-and-flyweight" caption="Figure 20.1: A shared node interface lets a parcel leaf and a consolidation branch answer total weight and parcel listing the same way, recursing through any depth of nesting. Two registries intern shared port and commodity codes." %}

## Pattern: Composite

### The problem

A routing routine receives a node and asks for its total weight and its parcels. The node might be one parcel, a consolidation of parcels, or a consolidation of consolidations, and the routine shouldn't have to check.

### The solution

A **Composite** composes objects into trees of part-whole hierarchies so a caller treats a single object and a composition the same way.

### How C# models it

The uniform surface is two members:

```csharp
public interface IConsignmentNode
{
    decimal TotalWeightKilograms { get; }

    IEnumerable<ShipmentParcel> Parcels();
}
```

The leaf is a record:

```csharp
public sealed record ShipmentParcel(string Reference, decimal WeightKilograms, PortCode Destination)
    : IConsignmentNode
{
    public decimal TotalWeightKilograms => WeightKilograms;

    public IEnumerable<ShipmentParcel> Parcels()
    {
        yield return this;
    }
}
```

A leaf's weight is its total, and the only parcel it yields is itself. It's a record because a parcel behaves like a value: two parcels with the same reference, weight and destination are the same parcel, and the generated equality says so with no hand-written `Equals`.

The branch recurses into its children:

```csharp
public sealed class Consolidation(string Reference, IReadOnlyList<IConsignmentNode> Children) : IConsignmentNode
{
    public string Reference { get; } = Reference;

    public IReadOnlyList<IConsignmentNode> Children { get; } = Children;

    public decimal TotalWeightKilograms => Children.Sum(child => child.TotalWeightKilograms);

    public IEnumerable<ShipmentParcel> Parcels() => Children.SelectMany(child => child.Parcels());
}
```

`Sum` asks each child for its own total without checking whether it's a parcel or another consolidation. A child consolidation sums its own children, and the recursion stops at a `ShipmentParcel`. `Parcels()` recurses the same way through `SelectMany`, flattening every descendant leaf into one sequence.

### Why the branch isn't a record

A positional record generates equality from its constructor parameters, and `Children` is an `IReadOnlyList<IConsignmentNode>`. A list doesn't override `Equals` to compare elements, so two consolidations over separately built lists with the same parcels would still compare unequal. A consolidation is identified by which node it is, so a class with reference equality states that directly.

### Depth stays invisible

| Tree | Result |
| --- | --- |
| One parcel to Los Angeles | Its own weight; `Parcels()` yields only itself |
| Two Rotterdam parcels nested in a consolidation with a Hamburg parcel | `TotalWeightKilograms` is 59.75, the sum of three leaves |
| Three levels, four Singapore parcels | `Parcels()` returns `PARCEL-A` through `PARCEL-D` |

Nothing in `Consolidation`, or in a caller, mentions depth. The interface is what stays uniform across levels.

## Pattern: Flyweight

### The problem

Thousands of parcels point at a few ports and commodities. Each one building its own `PortCode` multiplies identical objects, and nothing guarantees that two parcels bound for Rotterdam see the same port.

### The solution

A **Flyweight** shares one instance of the intrinsic state many objects have in common, and keeps the per-use state outside it.

### How C# models it

A port's UN/LOCODE and name are intrinsic: Rotterdam is `NLRTM` for every parcel. `PortCode` is the shared payload:

```csharp
public sealed class PortCode
{
    public PortCode(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }

    public string Name { get; }
}
```

`CommodityCode` mirrors it with `Code` and `Description` for a Harmonized System heading.

### Why the payload isn't a record

A record compares by value, so two separately built `PortCode("NLRTM", "Rotterdam")` records would be equal at different addresses. Today's registry returns one instance either way. The risk is a later change: a `Get` that builds a new `PortCode` on every call would still pass a value-equality check against a record, while breaking the one property Flyweight provides, that every parcel bound for a port points at the same object. With a plain class, default reference equality holds, and `Assert.Same` and `Assert.NotSame` check the actual guarantee.

### The registry

`PortCodeRegistry` builds the shared set once and serves every lookup from it:

```csharp
public sealed class PortCodeRegistry
{
    private static readonly PortCode[] SeedPorts =
    [
        new PortCode("NLRTM", "Rotterdam"),
        new PortCode("USLAX", "Los Angeles"),
        new PortCode("SGSIN", "Singapore"),
        new PortCode("DEHAM", "Hamburg"),
        new PortCode("CNSHA", "Shanghai"),
    ];

    private readonly FrozenDictionary<string, PortCode> _byCode =
        SeedPorts.ToFrozenDictionary(port => port.Code);

    public PortCode Get(string code) => _byCode.TryGetValue(code, out var port)
        ? port
        : throw new UnknownPortCodeException($"No port code is registered for '{code}'.");

    public bool TryGet(string code, out PortCode? port) => _byCode.TryGetValue(code, out port);
}
```

The five UN/LOCODEs stand in for a published reference table. `ToFrozenDictionary` runs once, when the registry is constructed. `FrozenDictionary` gives up adding or removing entries after construction in exchange for lookups tuned for a collection built once and read many times, which is the shape of a port set that never changes while the process runs. `CommodityCodeRegistry` is the same shape over four headings: 0901 coffee, 6109 knitted or crocheted T-shirts and similar garments, 8471 automatic data processing machines, and 2709 crude petroleum oils.

| Lookup | Result |
| --- | --- |
| `Get("NLRTM")` twice | The same instance |
| `Get` for Rotterdam and for Hamburg | Two different instances |
| `Get("ZZZZZ")` | Throws `UnknownPortCodeException` |
| `TryGet("CNSHA")` | `true`, with the port |
| `TryGet("ZZZZZ")` | `false`, with `null` |

Use `Get` where an unknown port is a data fault to stop on, and `TryGet` when validating a batch of incoming codes where a miss is routine. The commodity registry behaves the same way and throws `UnknownCommodityCodeException`, a separate type, so catching one never swallows the other.

{% include excalidraw.html name="20-flyweight-frozen-pool" caption="Figure 20.2: Port and commodity code registries each build a frozen lookup table once and serve every later lookup from it, returning the identical interned instance for a known code and throwing for an unrecognized one." %}

## When not to use it

- **Composite.** Skip it when nothing nests. A flat batch of parcels is an `IReadOnlyList<ShipmentParcel>`: one `Sum` gives the weight and the list already enumerates the parcels. A node interface and a branch type over one-level data add recursion the data never exercises.
- **Flyweight, for unique values.** A customs declaration number that exists once has no redundancy to remove. A registry around it adds a lookup, a seed set and an exception path with nothing to share.
- **Flyweight, for per-use state.** If a port's live congestion, or a commodity's duty rate on one shipment, must vary independently, one shared instance can't hold both values; mutating it changes every parcel's view. Put that value on `ShipmentParcel` or beside it, not on the interned instance.

> **🤖 Explore with your agent**
>
> "Keep `PortCode` a `sealed class` with get-only `Code` and `Name`, not a record, so reference equality shows that every parcel bound for `NLRTM` shares one instance. Seed `PortCodeRegistry` once with `ToFrozenDictionary`, make `Get` throw `UnknownPortCodeException` and make `TryGet` return `false` for an unknown code. Put nothing that varies per use, such as congestion, on `PortCode`. Keep `IConsignmentNode`, `Consolidation` and `ShipmentParcel` only because consolidations nest; a flat parcel list is a `Sum`."
>
> Run `dotnet test --project ConsignmentRouting.Tests --filter FlyweightRegistryTests`; its `Assert.Same` checks fail if `Get` builds a new `PortCode`.

## Build, run, observe

```sh
cd examples/20-composite-and-flyweight
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. Everything is in-memory data. All 11 tests pass: three for the tree (leaf, two-level sum, three-level flatten) and four each for the port and commodity registries (same instance, different instances, `Get` throwing, `TryGet` without throwing).

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Composite and Flyweight.
- Microsoft Learn, [`FrozenDictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2): the read-optimized structure `ToFrozenDictionary` builds into both registries.
- Microsoft Learn, [`System.Collections.Frozen` namespace](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen): when a frozen collection fits better than a regular `Dictionary` or an immutable collection type.
- Microsoft Learn, [Collections and data structures](https://learn.microsoft.com/en-us/dotnet/standard/collections/): the broader catalog, for comparing `FrozenDictionary` with `Dictionary<TKey,TValue>` and the `System.Collections.Immutable` types.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
