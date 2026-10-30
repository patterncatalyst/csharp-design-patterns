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

- Explain why `IConsignmentNode` lets a `ShipmentParcel` leaf and a
  `Consolidation` branch answer `TotalWeightKilograms` and `Parcels()`
  through the identical surface, so a caller never branches on which kind
  of node it is holding.
- Build `Consolidation` so it sums weight and flattens parcels recursively
  over children that may themselves be leaves or further consolidations,
  and show the same two members keep working no matter how many levels the
  tree actually has.
- Explain why `PortCode` and `CommodityCode` are plain sealed classes
  rather than records, and prove with `Assert.Same` and `Assert.NotSame`
  that a registry's `Get` method hands back one shared instance per code
  instead of a fresh, value-equal copy.
- Build a registry that seeds a `FrozenDictionary` once with
  `ToFrozenDictionary` and serves every later lookup from that same
  read-optimized structure, choosing between a throwing `Get` and a
  non-throwing `TryGet` depending on whether a missing code is a fault or
  a routine outcome.
- Recognize when a Composite tree adds traversal machinery a flat list
  already covers, and when an interning registry is the wrong reach for a
  value that is nearly unique or that needs to change independently per
  use.

## Why this matters

A bulk carrier rarely moves one parcel at a time. It groups parcels bound
through the same corridor into a consolidation, and it often nests one
consolidation inside another: parcels collected at a regional depot roll
up into a regional consolidation, several regional consolidations roll up
into a long-haul consolidation bound for one destination port, and that
long-haul consolidation might itself be one line item inside a still
larger multimodal shipment. Whatever asks how much a shipment weighs, or
which individual parcels are actually inside it, needs an answer whether
it is handed a single loose parcel or the outermost consolidation of a
three-level tree. Writing a separate weight calculation and a separate
parcel listing for a bare parcel, for a consolidation of parcels, and for
a consolidation of consolidations means a caller has to know, at every
call site, exactly how deep the structure in its hands goes, and that
knowledge breaks the moment someone adds one more level of nesting
somewhere else in the system. Composite closes that gap: give a leaf and a
branch the same interface, and a caller that only ever asks for a total
weight or a parcel listing never has to check which one it is holding.

Inside that same tree, the same port codes and the same commodity codes
repeat at a volume the carrier's own call list never changes much. Most of
a bulk carrier's volume moves through a handful of major ports, Rotterdam,
Los Angeles, Singapore, Hamburg, Shanghai, and a shipment tree can carry
thousands of parcels, most of them routed through that same small set.
Materializing a fresh `PortCode` object, with its own copy of the string
"Rotterdam" and the string "NLRTM", for every single parcel bound through
Rotterdam multiplies an allocation that should happen once per port the
carrier actually calls at. Harmonized System commodity codes follow the
identical shape: a manifest line for commodity 0901, coffee, repeats
across every shipment that carries coffee, and the code and its
description never vary from one coffee shipment to the next. Flyweight
answers that pattern directly: intern the small, fixed set of distinct
values a large population of parcels actually points at, build that set
once, and hand every parcel a reference into the shared set rather than a
private copy.

This chapter builds both patterns against one routing example because a
consolidated shipment tree is where the two problems meet. The tree itself
is the Composite: `IConsignmentNode` gives a `ShipmentParcel` leaf and a
`Consolidation` branch one interface to answer through. The destination
each leaf carries is the Flyweight: `PortCodeRegistry`, and its commodity
counterpart `CommodityCodeRegistry`, each intern one instance per code and
hand that same instance to every parcel that names it.

{% include excalidraw.html name="20-composite-and-flyweight" caption="Figure 20.1: IConsignmentNode gives ShipmentParcel and Consolidation the same TotalWeightKilograms and Parcels() surface, so a Consolidation sums and flattens its children recursively without ever checking whether a child is a leaf or another consolidation; PortCodeRegistry and CommodityCodeRegistry each build a FrozenDictionary once from a fixed seed set and hand back the one interned PortCode or CommodityCode instance a code resolves to, so every parcel naming the same port or commodity points at the same shared object." %}

## Composite: one interface for a leaf and a branch

The Gang of Four's Composite pattern composes objects into tree
structures that represent part-whole hierarchies, so a caller treats an
individual object and a composition of objects the same way. This
example's version of that uniform surface is `IConsignmentNode`:

```csharp
public interface IConsignmentNode
{
    decimal TotalWeightKilograms { get; }

    IEnumerable<ShipmentParcel> Parcels();
}
```

Two members, and every concrete node, a leaf or a branch, answers both the
same way regardless of what sits beneath it. `ShipmentParcel` is the leaf:

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

A leaf's own weight is its total weight, with nothing to sum, and the only
parcel reachable from a leaf is the leaf itself, yielded once.
`ShipmentParcel` is a record rather than a plain class because a parcel
behaves like a value: two parcels built from the same reference, weight,
and destination are the same parcel, and the equality a record generates
automatically from those three constructor parameters matches that, with
no hand-written `Equals` override anywhere in the type.
`CompositeTreeTests.A_leaf_parcel_reports_its_own_weight_and_yields_only_itself`
builds one `ShipmentParcel` against the Los Angeles port code and asserts
its `TotalWeightKilograms` equals its own `WeightKilograms`, and that
`Parcels()` returns a sequence containing only that one parcel,
`Assert.Equal([parcel], parcel.Parcels())`, checked by the record's
generated equality rather than by reference.

`Consolidation` is the branch, and it answers both members by recursing
into whatever its own children happen to be:

```csharp
public sealed class Consolidation(string Reference, IReadOnlyList<IConsignmentNode> Children) : IConsignmentNode
{
    public string Reference { get; } = Reference;

    public IReadOnlyList<IConsignmentNode> Children { get; } = Children;

    public decimal TotalWeightKilograms => Children.Sum(child => child.TotalWeightKilograms);

    public IEnumerable<ShipmentParcel> Parcels() => Children.SelectMany(child => child.Parcels());
}
```

The primary constructor on the class, not a record, promotes `Reference`
and `Children` straight into read-only properties, and
`TotalWeightKilograms` calls `TotalWeightKilograms` right back on every
child through `Sum`, with no check anywhere for whether a given child is a
`ShipmentParcel` leaf or another `Consolidation`. A child that is itself a
`Consolidation` answers `TotalWeightKilograms` by summing its own
children, which may include yet another `Consolidation`, and the
recursion bottoms out only when it reaches an actual `ShipmentParcel`,
where `TotalWeightKilograms` is the parcel's own weight with nothing left
to sum. `Parcels()` recurses the identical way through `SelectMany`,
flattening every descendant leaf into one sequence regardless of how many
branches sit between the outermost consolidation and the parcels at the
bottom.

`Consolidation` is a plain sealed class rather than a record for a reason
distinct from why `ShipmentParcel` is a record. A positional record
generates equality from its constructor parameters, but `Children` is an
`IReadOnlyList<IConsignmentNode>`, and a list does not override `Equals`
to compare its elements; two consolidations built from two separately
constructed lists holding the same parcels in the same order would still
compare unequal under a record's generated equality, because the
generated code calls `Equals` on the list reference itself, not on its
contents. Writing `Consolidation` as a class matches what that equality
gap already implies: a consolidation is identified by which node it is,
not by a structural comparison of its branches, and nothing is lost by
skipping equality machinery a record would generate but that the type
could never honor correctly against a list-typed property.

Two tests cover the recursive sum and the recursive flatten directly.
`CompositeTreeTests.A_nested_consolidation_sums_the_weight_of_every_leaf_parcel`
builds an inner consolidation of two parcels bound for Rotterdam, nests it
inside an outer consolidation alongside a third parcel bound for Hamburg,
and asserts the outer consolidation's `TotalWeightKilograms` equals 59.75,
the sum of all three leaves two levels down.
`Parcels_traverses_every_leaf_of_a_nested_tree` goes three levels deep, an
inner consolidation of two parcels nested inside a middle consolidation
holding a third parcel, nested inside an outer consolidation holding a
fourth, all four bound through Singapore, and asserts
`outerConsolidation.Parcels()` returns exactly four references,
`PARCEL-A` through `PARCEL-D`. Nothing in either test, or in the
`Consolidation` code that passes them, mentions how many levels deep the
tree actually runs; a routing routine that calls `TotalWeightKilograms` or
`Parcels()` on whatever `IConsignmentNode` it was handed gets the right
answer whether that node is a bare parcel or the root of an arbitrarily
deep consolidation tree, because the interface, not the caller, is what
stays uniform across every level.

## Flyweight: interning shared instances behind FrozenDictionary

The Gang of Four's Flyweight pattern separates a value's intrinsic state,
the part shared identically across every use, from whatever extrinsic
state varies per use site, and asks for exactly one shared instance of the
intrinsic part. A destination port is intrinsic state in this routing
tree: Rotterdam's UN/LOCODE, `NLRTM`, and its name never vary from one
parcel to the next, no matter how many parcels route through that port.
`PortCode` is the shared payload:

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

A plain reference type, sealed, with no `record` keyword anywhere.
`CommodityCode` mirrors it exactly, with `Code` and `Description` standing
in for a Harmonized System heading rather than a UN/LOCODE. Both are
written as ordinary classes rather than records, and that choice is what
makes the pattern's guarantee checkable. A record generates `Equals` from
its constructor parameters, so two separately constructed `PortCode`
records built from the same code and the same name would compare as equal
even though they sit at two different addresses; `Assert.Same` and
`Assert.NotSame`, which compare by reference, would keep passing against
either shape today, because the registry only ever hands back the one
instance it built at construction. What a record's structural equality
would hide is a later change that stops honoring that guarantee: a
rewritten `Get` that builds a new `PortCode("NLRTM", "Rotterdam")` on
every call instead of returning the interned one would still pass a test
written with `Assert.Equal` against a record, since the new instance
compares equal to the old one by value, while quietly breaking the one
property Flyweight exists to provide, that every parcel bound for the
same port points at the identical object in memory. Writing `PortCode`
and `CommodityCode` as plain classes keeps the default reference equality
in force, so `Assert.Same` and `Assert.NotSame` test the actual guarantee
the registry makes rather than a value comparison that would mask the
moment that guarantee stopped holding.

`PortCodeRegistry` builds the shared set once and serves every lookup from
it:

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

`SeedPorts` is the fixed, small list of ports this carrier actually calls
at, five UN/LOCODEs standing in for whatever a real routing system would
load from a published reference table. `ToFrozenDictionary` runs once, in
the field initializer for `_byCode`, the moment a `PortCodeRegistry` is
constructed, turning that array into a `FrozenDictionary<string,
PortCode>` keyed by code. `FrozenDictionary` trades a mutation path a
regular `Dictionary` keeps open, adding or removing entries after
construction, for lookup performance tuned for a collection built once
and read many times afterward; a routing system that resolves a port code
on every parcel it processes, against a set of ports that never changes
while the process is running, is the shape `FrozenDictionary` targets
directly. `CommodityCodeRegistry` is the identical shape closed over four
Harmonized System headings instead of five UN/LOCODEs: 0901 for coffee,
6109 for knitted or crocheted T-shirts and similar garments, 8471 for
automatic data processing machines, and 2709 for crude petroleum oils,
each description taken straight from its heading.

`Get` and `TryGet` give a caller two different ways to handle the same
miss. `FlyweightRegistryTests.PortCodeRegistry_returns_the_same_instance_on_repeated_lookups`
calls `Get("NLRTM")` twice against one registry and asserts the two
results with `Assert.Same`, the identical object both times, not merely
two objects that happen to compare equal.
`PortCodeRegistry_returns_different_instances_for_different_codes`
resolves Rotterdam and Hamburg from the same registry and asserts
`Assert.NotSame`, two distinct instances for two distinct codes.
`PortCodeRegistry_Get_throws_for_an_unknown_code` asks for `"ZZZZZ"` and
asserts `Get` throws `UnknownPortCodeException`, the shape that fits a
caller for whom an unrecognized port is a fault to stop on immediately: a
parcel that named a port the registry never interned is a data problem
the caller should not quietly route around.
`PortCodeRegistry_TryGet_distinguishes_a_known_code_from_an_unknown_one`
calls `TryGet` once against `"CNSHA"` and once against `"ZZZZZ"` on the
same registry, asserting the first call returns `true` with a non-null
`PortCode` and the second returns `false` with the out parameter left
`null`, the shape that fits a caller validating a batch of incoming codes
where a miss is an expected, routine outcome rather than something to
throw over. The four `CommodityCodeRegistry_*` cases in the same file
mirror all four against `CommodityCodeRegistry`, down to
`CommodityCodeRegistry_Get_throws_for_an_unknown_code` asserting
`UnknownCommodityCodeException` specifically, a different exception type
than the port registry throws, so a caller catching one never accidentally
swallows the other.

## Build, run, observe

Every type in this example is in-memory data: no network call and no file
read, only `PortCode` and `CommodityCode` instances built into two small
`FrozenDictionary` instances, and `Consolidation` trees built over
`ShipmentParcel` leaves. The suite needs nothing beyond the local .NET 10
SDK. The example is standalone-testable from its own directory:

```sh
cd examples/20-composite-and-flyweight
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 11
  failed: 0
  succeeded: 11
  skipped: 0
  duration: 1s 710ms
```

The 11 cases split across two files. `CompositeTreeTests` holds three
cases: a two-level nested consolidation summing to 59.75 kilograms across
three leaves, a three-level nested consolidation flattening to exactly
four parcels through `Parcels()`, and a single leaf parcel reporting its
own weight and yielding only itself. `FlyweightRegistryTests` holds the
remaining eight, four proving `PortCodeRegistry`'s guarantees and four
proving the identical guarantees against `CommodityCodeRegistry`: a
repeated lookup of the same code returns the same instance, two different
codes return two different instances, an unknown code throws the
registry's own exception type from `Get`, and `TryGet` tells a known code
from an unknown one without throwing either way.

## When not to use it

A Composite interface costs a shared abstraction, `IConsignmentNode` here,
that both a leaf type and a branch type have to implement and that every
caller has to program against instead of a concrete type it already
knows. That cost buys something only once real nesting exists or is
clearly coming. A system that only ever hands a flat batch of parcels to a
routing routine, with no consolidation step anywhere between a parcel and
the shipment it rides in, gets nothing from `Consolidation` or the
interface wrapping it: a plain `IReadOnlyList<ShipmentParcel>` already
answers "what is the total weight" with one `Sum` call and is already an
enumerable sequence of parcels, with no tree to flatten because there
never was one. Introducing `IConsignmentNode` and a `Consolidation` branch
type for data that will only ever be one level deep adds a traversal
abstraction over a structure that never actually branches, and every
future reader of that code has to follow recursion the data itself will
never exercise.

Interning pays off specifically when a large population of objects would
otherwise hold redundant copies of the same small set of values, and it
stops paying off once either half of that trade disappears. A one-off
reference string stamped onto a single shipment, a customs declaration
number that exists exactly once and is never repeated across any other
parcel, has no redundancy for a registry to remove; building a registry
around a value with exactly one consumer adds a lookup layer, a seed set,
and a missing-key exception path with nothing behind any of it to share.
Flyweight also breaks down the moment the shared value needs to vary per
use rather than per code: if a port's live congestion status, or a
commodity's duty rate on a given day, had to change independently for one
shipment without affecting every other shipment pointing at the same
`PortCode` or `CommodityCode`, a single shared instance could not hold
both facts at once, since mutating the one object every parcel shares
would corrupt every other parcel's view of the same port or commodity at
the same moment. A value that needs to vary per use belongs on the object
using it, a field on `ShipmentParcel` itself or alongside it, not folded
into the interned instance every unrelated parcel also points at.

> **AI sidebar.** Asked to model a shipment's parcels, an AI pairing
> partner will sometimes produce a full Composite tree, a node interface,
> a leaf type, and a branch type, for data the request never described as
> nesting at all: one shipment with a flat list of parcels and no grouping
> step anywhere between a parcel and the shipment it belongs to. The tree
> compiles and the single-level case runs through it correctly, since a
> one-item `Consolidation` recurses exactly once, but every call on it now
> walks an interface and a `SelectMany` chain to answer a question a
> direct `Sum` over a list already answered. A second common suggestion,
> asked to share a repeated value like a port or a commodity, is a
> registry built correctly at first and then widened later to let a
> caller update a cached entry in place, a `SetCongestionLevel` method
> added straight onto `PortCode`, once some other feature needs a
> port-specific value to change. That addition reintroduces the exact bug
> Flyweight is built to avoid: mutating the one shared `PortCode` instance
> for Rotterdam changes what every parcel pointing at it sees, including
> parcels that have nothing to do with whatever shipment triggered the
> update. Check a generated registry against the question this chapter
> answers directly: does the value being interned ever need to change
> independently per use, and if it does, that value does not belong on
> the shared instance at all, no matter how convenient one more property
> on an already-shared object looks in the moment.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Composite and Flyweight this chapter builds on.
- Microsoft Learn, [`FrozenDictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2): the authority for the read-optimized guarantee `ToFrozenDictionary` builds into `PortCodeRegistry` and `CommodityCodeRegistry`.
- Microsoft Learn, [`System.Collections.Frozen` namespace](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen): the source for when a frozen collection fits better than a regular `Dictionary` or an immutable collection type.
- Microsoft Learn, [Collections and data structures](https://learn.microsoft.com/en-us/dotnet/standard/collections/): the broader catalog `FrozenDictionary` belongs to, for comparing it against `Dictionary<TKey,TValue>` and the `System.Collections.Immutable` types.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
