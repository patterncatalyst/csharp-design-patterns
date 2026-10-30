# 20-composite-and-flyweight

Two structural patterns sharing one consignment-routing domain:

- **Composite.** `IConsignmentNode` is the uniform interface both a leaf and a
  branch implement. `ShipmentParcel` is the leaf: a single parcel bound for a
  `PortCode`, whose `TotalWeightKilograms` is its own weight and whose
  `Parcels()` yields only itself. `Consolidation` is the branch: a named
  grouping of child nodes, each of which may be another parcel or another
  consolidation. `TotalWeightKilograms` sums every child recursively, and
  `Parcels()` flattens every descendant leaf, so a routing routine written
  against `IConsignmentNode` never branches on how deep the tree it was
  handed actually is.
- **Flyweight.** `PortCode` and `CommodityCode` are the shared, interned
  payloads: a port or a commodity code is looked up once and the same
  instance is handed back to every parcel that names it, rather than each
  parcel carrying its own private copy of a value that is identical across
  the whole consignment. `PortCodeRegistry` and `CommodityCodeRegistry` build
  their seed set into a `FrozenDictionary` with `ToFrozenDictionary`, the
  read-optimized collection .NET ships for exactly this shape: built once,
  read many times, never mutated again. Both are plain reference types, not
  records, because the pattern depends on reference equality: two parcels
  bound for Rotterdam share the one `PortCode` instance the registry
  interned, they do not merely hold two value-equal copies of it.

**What the tests prove.** `CompositeTreeTests` builds a consolidation nested
inside another consolidation and asserts the outer node's
`TotalWeightKilograms` equals the sum of every leaf parcel's own weight, that
`Parcels()` reaches all four leaves of a three-level tree by reference and by
count, and that a single leaf parcel reports its own weight and yields only
itself. `FlyweightRegistryTests` asserts `PortCodeRegistry.Get` returns the
identical instance on repeated calls for the same code (`Assert.Same`), a
different code returns a different instance (`Assert.NotSame`), an unknown
code throws `UnknownPortCodeException`, and `TryGet` reports `false` with a
`null` output for an unknown code and `true` with a non-null output for a
known one; `CommodityCodeRegistry` is proven the same way, the second
instance of the same Flyweight shape over Harmonized System codes.

Shipping domain, no containers: the seed ports are five UN/LOCODEs
(Rotterdam, Los Angeles, Singapore, Hamburg, Shanghai) and the seed
commodities are four Harmonized System headings (coffee, knitted T-shirts,
automatic data processing machines, crude petroleum oils), both small, fixed
sets built once at construction, no network or I/O to fake out.

## Build and test

```sh
cd examples/20-composite-and-flyweight
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
