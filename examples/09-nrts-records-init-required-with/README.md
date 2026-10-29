# 09-nrts-records-init-required-with

A shipping-domain `Consignment`, built with nullable reference types, records,
and `init`/`required`/`with` to show how the four features cooperate.

**The domain types.** `Consignment` is a `sealed record` (a record **class**,
not a struct), declared without a primary constructor: every member is a
property with `get; init;`, and every non-nullable member is marked
`required`. `ConsignmentId`, `Route`, and `Weight` are `required`;
`ReferenceNumber` is `string?`, legitimately absent on a newly booked
consignment; `DeclaredValue` is non-nullable but not `required`, defaulting to
`Money.Zero` to show that a default value and a `required` member coexist on
the same type without either one defeating the other. `Route` is a positional
`sealed record Route(string Origin, string Destination)`. `Weight` and `Money`
are fresh `readonly record struct` types (not reused from the value-types
example), each with a validating property that shadows its primary
constructor's parameter.

**Why a record class here.** A record struct copies by value on every
assignment; a record class is a reference type with structural equality
layered on top. `Consignment` is the right shape for the second case: it is
large enough that copying it on every assignment would be wasteful, and the
point of this chapter is reference-type value equality, not copy semantics.
Equality still recurses structurally: two independently constructed
`Consignment` values with identical property values are `Equals`, share a
hash code, and compare equal through the compiler-generated `==`, and that
comparison recurses into `Route` (itself a record) and `Weight`/`Money`
(record structs).

**`required` versus nullable.** Nullable reference types and `required` solve
two different problems that are easy to conflate. `string?` on
`ReferenceNumber` tells the compiler a value is allowed to be absent at every
point in the program, including after construction. `required` on
`ConsignmentId`, `Route`, and `Weight` tells the compiler a value must be
supplied at construction, regardless of whether its type is nullable or not;
the compiler rejects an object initializer that leaves a `required` member
unset with error `CS9035`. The following does not compile:

```csharp
// CS9035: Required member 'Consignment.Route' must be set.
var broken = new Consignment
{
    ConsignmentId = "CNS-4217",
    Weight = new Weight(1250m),
};
```

That is a compile error, not a runtime failure, so there is no test for it in
this example; `RequiredMembersTests` instead proves the positive case, that
an initializer setting every required member compiles and constructs
correctly.

**`with` and amendments.** `Consignment` exposes three amendment helpers,
`ReRouteTo`, `WithCorrectedWeight`, and `WithReferenceNumber`, each a one-line
`with` expression that returns a new `Consignment` and leaves the receiver
untouched. `ReRouteTo` nests one level, producing a new `Route` through its
own `with` before assigning it to the outer `with`. Not every amendment needs
a dedicated helper: `WithAmendmentTests` also amends `DeclaredValue` with a
bare `with` expression at the call site, showing that the type does not have
to wrap every possible change in a named method.

## What the tests prove

`ValueEqualityTests` shows two independently constructed `Consignment` values
with identical data are `Equals`, `==`, and share a hash code, that changing
the nested `Route` or the `Weight` breaks equality, and that `Route`,
`Weight`, and `Money` each carry their own generated value equality.
`WithAmendmentTests` proves each amendment helper (and the bare `with` at the
`DeclaredValue` call site) produces a new value while the original is
unchanged, and that re-routing a consignment back to its starting destination
produces a value equal to the original. `NullabilityTests` proves
`ReferenceNumber` is `null` on a freshly constructed consignment and non-null
after `WithReferenceNumber`, and that the required, non-nullable members
never turn null across a chain of `with`-based amendments.
`RequiredMembersTests` proves an object initializer that sets every required
member compiles and constructs a correct `Consignment`.

## Build and test

```sh
cd examples/09-nrts-records-init-required-with
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
