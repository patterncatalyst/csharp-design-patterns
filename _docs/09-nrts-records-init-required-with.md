---
title: "NRTs, records, and init/required/with"
order: 9
layout: tutorial
sectionid: docs
part: "dotnet-substrate"
description: "Immutability as a design tool, and why nullable reference types remove the motivation for the Null Object pattern."
---

<!-- EXAMPLE (shipping domain): Consignment as a record, with the with expression for amendments. -->

## Learning objectives

By the end of this chapter you can:

- Explain what compiler-generated equality a record class provides, and
  how that equality recurses through a nested positional record and nested
  record structs rather than stopping at the outer type.
- Pair `required` with `init` to guarantee every non-nullable property on a
  record is set before construction completes, and explain why that guarantee
  is what actually closes the hole nullable reference types leave open on
  their own.
- Produce an amended copy of an immutable record with a `with` expression,
  including a `with` expression nested one level into a member that is itself
  a record, and state precisely why `with` on a record class is a shallow
  copy.
- Distinguish a legitimately absent member modeled as a nullable reference
  type from a non-nullable member that must never hold `null`, and explain
  why nullable reference types remove the reflex to reach for a Null Object.
- State the compile-time failure that results from omitting a `required`
  member from an object initializer, and explain why that guarantee belongs
  to the compiler rather than to a runtime check.

## Why this matters

A consignment isn't a fact fixed at booking. A customs broker corrects a
declared weight after a re-weigh at the depot. A dispatcher re-routes a
shipment around a closed border crossing. A customer calls back two days
later with a purchase-order number to attach. None of these create a new
consignment; they amend one that already exists.

A system that represents a consignment as a plain mutable class, handed
around by reference, invites the bug class mutation always invites. Two
parts of the system each hold what they believe is "the" consignment. One
calls a setter to correct the weight. The other silently sees the change
too, ready or not, because there was only ever one object and every
reference pointed at the same mutable state. A rating engine that captured
a weight for billing before the correction lands has no record of what it
actually billed against. The object it held a reference to changed out
from under it.

Modeling `Consignment` as an immutable value removes that failure mode
structurally, not procedurally. Booking a consignment produces one value.
Correcting the weight doesn't mutate it. It produces a second, independent
value that differs only in the field that changed, while the first value,
and everyone still holding a reference to it, stays unaffected. A re-route
produces a third value. Nothing downstream has to guess whether the object
it's looking at might change underneath it, because it can't: the only way
to get a different `Consignment` is to ask for one explicitly, through an
operation that returns a new value. That guarantee comes from the record
declaration itself, not from documentation or convention, and this
chapter's second half shows why.

The other half of the picture is what a `Consignment` is allowed to hold.
A `ConsignmentId` and a `Route` must always be present; a shipment with no
route has nowhere to go. Treating that as a possibility every downstream
method handles defensively, a `null`-checked `Route` property, is pure
accidental complexity. A `ReferenceNumber` is different: it's
sometimes absent, a customer-supplied purchase-order code that may never
arrive.

Before nullable reference types, both facts lived by convention, enforced
if at all by a hand-written constructor and scattered comments. The type
system couldn't tell a reader, or the compiler, which properties were safe
to dereference without a check and which weren't. Nullable reference types
turn that distinction into part of the type itself, and `required` closes
the one gap nullability alone can't: a property the compiler knows can
never be `null`, but that nothing forces a caller to set. Together,
`required`, `init`, `with`, and nullable reference types let `Consignment`
state its own contract at the declaration and hold every caller to it.

{% include excalidraw.html name="09-nrts-records-init-required-with" caption="Figure 9.1: Amending a consignment record copies it into a new instance with only the named members overridden, leaving the original untouched; equality compares values, not references." %}

## Records and recursive value equality

`Consignment` is a `sealed record`, which makes it a reference type, not a
value type: assigning one `Consignment` variable to another copies a
reference, the same as assigning any class. What a record adds is
compiler-generated structural equality. `Consignment` is declared without a
primary constructor, with every member spelled out as its own property:

```csharp
public sealed record Consignment
{
    public required string ConsignmentId { get; init; }      // #A
    public required Route Route { get; init; }                // #A
    public required Weight Weight { get; init; }               // #A
    public string? ReferenceNumber { get; init; }               // #B
    public Money DeclaredValue { get; init; } = Money.Zero;     // #C
}
```

`#A` Required: an initializer that omits any of these three fails to
compile.
`#B` Nullable: legitimately absent, and defaults to `null`.
`#C` Non-nullable but not required: `Money.Zero` already satisfies the
contract without a caller supplying one.

Named properties read more clearly here than a positional record would.
`Route`'s two short members fit a primary constructor naturally; a
five-member type with three different enforcement rules per member
doesn't. Either way, the `record` keyword does the same work. It
synthesizes `Equals(Consignment? other)`, the `IEquatable<Consignment>`
implementation behind it, `GetHashCode`, `operator ==`, `operator !=`, and
a copy constructor, all derived from the declared properties.
`ValueEqualityTests` proves those generated members are correct, not just
present.
`Two_independently_constructed_consignments_with_identical_data_are_equal`
builds two separate `Consignment` instances from the same field values and
asserts `Equals`, `==`, and a shared `GetHashCode()` all agree the two are
the same consignment, even though they're two different objects on the
heap sharing no reference at all.

The generated equality doesn't stop at `Consignment`'s own five properties.
It recurses into every member that knows how to compare itself. `Route` is
`public sealed record Route(string Origin, string Destination)`, a
positional record with its own generated equality over its two strings,
and `Weight` and `Money` are each a `readonly record struct` with equality
over their own fields. When `Consignment.Equals` compares two `Route`
values, it calls `Route`'s own `Equals`, which compares `Origin` and
`Destination` by value, not by reference. The same holds when it compares
two `Weight` values, through `Weight`'s own equality over `Kilograms`.
`Changing_the_nested_route_makes_consignments_unequal` and
`Changing_the_weight_makes_consignments_unequal` both confirm this:
changing only the nested `Route.Destination`, or only the nested
`Weight.Kilograms`, on an otherwise identical consignment is enough to make
the two compare unequal. The comparison is structural all the way down,
not just a comparison of `Consignment`'s own top-level fields.

That recursion carries one caveat: a record's generated equality calls
`Equals` on each member, but it can't upgrade a member's own equality; it
can only use whatever `Equals` that member's type actually provides.
`Route`, `Weight`, and `Money` are all records themselves, so the recursion
stays structural the whole way down here. A hypothetical
`CustomsDeclaration` class with no `record` modifier and no hand-written
`Equals` override would fall back to the reference equality every plain
class gets from `object`. `Consignment`'s generated equality would then
compare two `CustomsDeclaration` references, not their contents, so two
consignments holding separately constructed but field-identical
declarations would compare unequal despite looking, to a reader, exactly
alike. The fix isn't a special case in `Consignment`'s equality. It's
choosing a record, or a type with its own correct value equality, for
every member that needs to participate in structural comparison. [The C#
record types
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records)
states the rule directly: a record's generated equality compares "the
values of all the fields and properties of the object," only as deep as
the equality each individual field or property type provides.

## required and init: closing the construction gap

Before `required` existed, a non-nullable property on a type with no
constructor forcing its assignment left a reader, and the compiler, in an
uncomfortable position. Nullable reference types flag a non-nullable
property a constructor never assigns with a "possibly null" warning, when
the compiler can't prove every code path sets it. But a warning isn't an
error. A developer can see it, decide it's noise, and ship anyway. The
property can still end up holding `null` at runtime despite its declared
type saying it never would.

The usual fix before C# 11 was a hand-written constructor that took every
non-nullable member as a parameter and assigned it. That traded a compile
warning for actual enforcement, at the cost of a constructor that has to be
kept in sync by hand every time a property is added or removed.

`required` removes that trade entirely. A property marked `required` must
be set by every object initializer that constructs the type. The compiler
enforces this as a hard error, not a suppressible warning.
`ConsignmentId`, `Route`, and `Weight` are each declared `public required
string ConsignmentId { get; init; }` and its siblings; no constructor
anywhere in `Consignment` does the assigning instead. Leaving one out
doesn't compile:

```csharp
// CS9035: Required member 'Consignment.Route' must be set.
var broken = new Consignment
{
    ConsignmentId = "CNS-4217",
    Weight = new Weight(1250m),
};
```

That failure happens during compilation, before the program containing it
can run at all. That's exactly why this example has no test asserting it:
a test can only exercise code that compiles. `RequiredMembersTests`
instead proves the positive path: an initializer setting every required
member compiles cleanly and produces a `Consignment` whose properties hold
exactly the values supplied. `ReferenceNumber` defaults to `null` and
`DeclaredValue` defaults to `Money.Zero`, the two members the initializer
leaves unset.

`init` is the other half of the pairing, and it answers a different
question: not whether a value must be supplied, but whether it can ever
change after construction. An `init` accessor, [the documentation
explains](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init),
"assigns a value to the property or the indexer element only during object
construction," through either a constructor or an object initializer. Any
attempt to assign to it afterward, `consignment.Route = newRoute` from
outside the type, is a compile error.

Every property on `Consignment` uses `init` rather than `set`, which is
what makes the type's immutability a compile-time guarantee instead of a
convention a reader has to trust. Nothing in the codebase, including code
written after this chapter, can reach into an existing `Consignment` and
change a field in place. The language gives it no accessor capable of
doing so.

`required` and a default value aren't in tension, and `DeclaredValue`
demonstrates exactly that: `public Money DeclaredValue { get; init; } =
Money.Zero;` is non-nullable, like `Route` and `Weight`, but not
`required`, because it already has a value, `Money.Zero`, that satisfies
the contract without a caller supplying one. An initializer that omits
`DeclaredValue` compiles fine and produces a consignment with a declared
value of zero euros, a legitimate value for a consignment nobody has
valued yet, not a stand-in for absence.

The distinction `required` draws isn't "nullable versus non-nullable";
it's "has a type-owned default versus has no sensible value until a caller
supplies one." `ConsignmentId` has no default string that would mean
anything. A blank or placeholder identifier would be actively wrong, which
is exactly the case `required` exists to force a caller to settle at
construction.

{% include excalidraw.html name="09-required-init-construction-gap" caption="Figure 9.2: An object initializer that sets every required member compiles; one that omits a required member fails at compile time instead of risking a runtime null." %}

## Non-destructive amendment with with

`Consignment` exposes three amendment methods, each a single-expression
wrapper around a `with` expression that returns a new value and leaves the
receiver untouched:

```csharp
public Consignment ReRouteTo(string destination) =>
    this with { Route = Route with { Destination = destination } };   // #A

public Consignment WithCorrectedWeight(Weight weight) =>
    this with { Weight = weight };                                     // #B

public Consignment WithReferenceNumber(string referenceNumber) =>
    this with { ReferenceNumber = referenceNumber };                   // #B
```

`#A` A nested `with`: the inner expression amends `Route` first, and the
outer expression takes the result as the new `Route`.
`#B` A single-level `with`: overrides one member on `Consignment`
directly.

`WithAmendmentTests` proves what each method actually does, not just that
"returns a new value" sounds right.
`ReRouteTo_returns_a_new_consignment_and_leaves_the_original_unchanged`
re-routes a consignment from Hamburg to Antwerp, then checks both values:
the amended consignment's `Route.Destination` reads `"Antwerp"`, and the
original's still reads `"Hamburg"`. `WithCorrectedWeight` and
`WithReferenceNumber` get the same treatment. A fifth test,
`Declared_value_can_be_amended_with_a_bare_with_expression_at_the_call_site`,
amends `DeclaredValue` directly from the test, `original with {
DeclaredValue = new Money(18_500m, "EUR") }`, with no dedicated helper
method at all. A type doesn't have to wrap every amendment in a named
method for `with` to stay useful: a one-off amendment at a call site works
the same way a named helper does, since both desugar to the same
copy-and-override operation.

Split into two steps, `ReRouteTo`'s nested `with` shows its mechanics
directly:

```csharp
Route reRouted = Route with { Destination = destination };   // #A
Consignment amended = this with { Route = reRouted };         // #B
```

`#A` The inner `with` runs first, producing a new `Route` with only
`Destination` overridden; the current `Route`'s own `Origin` is untouched.
`#B` The outer `with` takes that new `Route` as the replacement value for
`Consignment.Route`. [The `with` expression
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression)
describes both steps as calling a compiler-synthesized copy constructor on
the receiver and then applying the named property assignments to the copy.

That copy is shallow, and the documentation says so directly: "the result
of a `with` expression is a shallow copy." A reference-typed member is
copied as a reference, not cloned. Here that's safe because `Route`,
`Weight`, and `Money` are themselves immutable records: the outer `with`
copies a reference to a `Route` value that can never change underneath
either the original or the amended consignment, since `Route` has no
setter anywhere on it. If `Route` were a mutable class instead, the same
shallow copy would leave the original and the amended consignment pointing
at the same `Route` object, and a mutation through either reference would
silently change what the other reports, reproducing the aliasing bug
immutability was meant to remove. Modeling `Route`, `Weight`, and `Money`
as records and record structs isn't a style preference separate from the
`with`-based design; it's what makes that design sound.
`An_amendment_that_reproduces_the_original_data_compares_equal_to_it`
closes the loop: re-routing a consignment back to its starting destination
produces a value that compares equal to the original through `==`, even
though it's a distinct object, because equality here is about the data,
not which instance produced it.

## Nullable reference types: absence versus never

`ReferenceNumber` is declared `string?`, and that single character carries
real meaning: a newly booked consignment has no purchase-order reference
yet, and that's not an error condition to work around. It's an expected,
ordinary state. `NullabilityTests.ReferenceNumber_is_null_on_a_freshly_constructed_consignment`
confirms exactly that: a `Consignment` built without a `ReferenceNumber`
compiles fine, since the property is nullable rather than `required`. Its
value reads `null` until a customer supplies one through
`WithReferenceNumber`.
`ReferenceNumber_is_non_null_after_an_amendment_that_adds_it` confirms
that amendment produces a consignment whose `ReferenceNumber` now holds
the supplied value. [The nullable reference types
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types)
frames the feature as annotations the compiler uses "to help you find
potential null reference errors," and the annotation here does exactly
that job: it tells every caller, through the type itself, that checking
`ReferenceNumber` for `null` before using it isn't defensive paranoia. It's
the type's actual contract.

Every other member on `Consignment` draws the opposite line.
`ConsignmentId`, `Route`, and `Weight` are declared without the `?`. That
tells both the compiler and a reader that a legitimate `Consignment` never
holds `null` in any of them. `required` is what turns that declared intent
into an enforced guarantee instead of a hopeful assertion.
`Required_non_nullable_members_never_turn_null_across_a_with_expression`
chains two amendments, a re-route followed by a weight correction, and
confirms `ConsignmentId` and `Route` are still non-null and correct
afterward. Nothing about a `with` expression, nested or not, has any path
that could leave a non-nullable, `required` member holding `null`: every
`with` expression starts from a fully valid `Consignment` and only ever
overrides the members it names.

This pairing matters more than either half alone. Nullable reference types
describe which members are allowed to be absent, and `required` guarantees
every member that isn't allowed to be absent actually has a value the
moment construction finishes.

Before both existed together, a team reached for a different fix for the
same problem: the Null Object pattern. A type like `Route` would ship with
a static `Route.None` or `Route.Empty` sentinel standing in for "no route
yet." Code calling `route.Origin` never had to null-check first, because
the sentinel supplied harmless placeholder values instead of `null`. That
pattern earns its cost specifically when a language can't
distinguish "guaranteed present" from "might be absent" in the type system
itself. A sentinel then has to carry the weight a type annotation can't.

With nullable reference types and `required` together, the distinction *is*
in the type system. `Route` on `Consignment` is `required`, so it's simply
never missing, and no sentinel stands in for a state the type can't be in.
`ReferenceNumber` is sometimes missing, so it's `string?`, and the correct
way to represent that is `null`, checked once at the point of use, not a
placeholder object pretending to be a reference number nobody supplied.
The reflex to reach for a Null Object loses its motivation once the type
system can say "this is required" and "this is optional" directly, and the
compiler holds every caller to whichever one is true.

## Build, run, observe

The chapter makes four claims: `Consignment`'s generated equality is
correct and recurses through nested records and record structs; every
amendment returns a new value and leaves the original untouched; the
`required`, non-nullable members never turn `null` across a `with`-based
amendment; and construction can't produce a `Consignment` with a `required`
member left out. Run the suite from the example's own directory, no container
in the loop, to watch them hold:

```sh
cd examples/09-nrts-records-init-required-with
dotnet test
```

It's also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 15
  failed: 0
  succeeded: 15
  skipped: 0
  duration: 911ms
```

The 15 cases split across four files, each tied to one of the chapter's
claims:

- **Equality is correct and recurses.** `ValueEqualityTests` confirms
  independently constructed values with identical data are equal and
  share a hash code, and that changing either the nested `Route` or the
  nested `Weight` alone makes two otherwise-identical consignments compare
  unequal, which is only possible if equality recurses past
  `Consignment`'s own top-level fields.
- **Amendments return new values, untouched originals.** `WithAmendmentTests`
  confirms every amendment helper, and the bare `with` expression amending
  `DeclaredValue` at the test's own call site, leaves the receiver
  unchanged, and that re-routing a consignment back to its starting
  destination produces a value equal to the one it started from.
- **Required members never turn null.** `NullabilityTests` confirms
  `ReferenceNumber` reads `null` on a freshly built consignment and
  non-null once an amendment supplies one, and that `required`,
  non-nullable members stay non-null across a chain of `with`-based
  amendments.
- **Construction enforces the contract.** `RequiredMembersTests` confirms
  an object initializer that sets every required member compiles, and
  that the two optional members, `ReferenceNumber` and `DeclaredValue`,
  correctly fall back to `null` and `Money.Zero`.

## When not to use it

Records, `init`-only immutability, and `with`-based amendment earn their
keep when a type's job is to represent a value: something two instances
can be the same by virtue of holding the same data, with no further
question of "which one." That's not every type a shipping system needs.

A consignment tracked live through a warehouse management system, one with
a database-assigned identity, a current physical location, and a change
history an operator can audit, needs identity semantics a record actively
works against. Two tracking rows describing the same physical shipment at
two different moments would have different field values, so they'd compare
unequal under a record's structural equality, even though they're "the
same consignment" in every sense that matters to that system. An
entity-tracking layer such as an ORM's change tracker also depends on
holding one canonical, mutable instance per tracked row and noticing when
its properties change. A type that hands back a new, independent value on
every amendment gives a change tracker nothing to watch, since nothing
ever mutates in place for it to observe. Identity-bearing types like that
belong on a class compared by a stable key, not on a record compared by
every field it carries.

The shallow-copy behavior of `with` is a second boundary, and it doesn't
announce itself as a problem until a nested member happens to be mutable.
This chapter's design works because `Route`, `Weight`, and `Money` are
themselves immutable, so sharing a reference to one across an original and
its amended copy is harmless.

A larger object graph that mixes immutable records with ordinary mutable
classes loses that safety the moment a `with` expression copies a
reference to one of the mutable parts. The "amended copy" and the original
end up pointing at the same mutable object for every member the `with`
expression didn't explicitly override, and a later mutation through either
path leaks into both, defeating the point of non-destructive amendment.
The fix isn't avoiding `with`; it's keeping every member that participates
in it immutable too, the same discipline `Consignment` already follows,
rather than reaching for a record around a mix of mutable and immutable
parts and trusting the shallow copy to behave like a deep one.

A third boundary is pure cost under sustained pressure. Each `with`
expression allocates a new `Consignment` on the heap. A batch correction
replaying ten thousand historical weight adjustments through
`WithCorrectedWeight` in a tight loop allocates ten thousand objects.
Almost all of them are garbage the instant the next one replaces it as
"the current value."

For a consignment amended a handful of times over its whole lifetime,
re-routed once, corrected once, a reference number attached once, that
allocation cost is immaterial against the bugs it prevents. For a hot path
reprocessing the same value many times a second, a carefully encapsulated
mutable type, with setters kept private and changes routed through methods
that preserve the type's invariants, can be the better trade.
Immutability is a design default here, not an unconditional rule every
value-shaped type must follow regardless of how often it changes.

<blockquote class="ai-sidebar" markdown="1">
Asked to fix a nullable warning on a non-nullable property, or to model a
value that's "sometimes missing," a generated answer often reaches for a
shortcut: adding `?` or `null!` to silence the warning, or an `Empty`/`None`
sentinel object instead of `null`.

> Check every non-nullable property here. If a value must always be
> present, make it `required` rather than adding `?` or `null!` to quiet
> the compiler. If it's truly optional, make it nullable and check for
> `null` at the point of use, not a sentinel object.

If the reply adds `?` or `null!` just to silence a warning, or introduces a
sentinel, ask for the `required`/nullable form instead.
</blockquote>

## Where this leaves you

You can now:

- Explain what `Consignment`'s generated equality compares, and why it
  recurses through `Route`'s own equality and `Weight`/`Money`'s own
  equality instead of stopping at `Consignment`'s top-level fields
  (`ValueEqualityTests`).
- Pair `required` with `init` on `ConsignmentId`, `Route`, and `Weight`,
  and explain why that pairing turns a "possibly null" warning into a hard
  compile-time failure an object initializer can't suppress.
- Write a `with` expression that amends one member, and a nested one like
  `ReRouteTo`'s that amends a member's own member, and state why both are
  shallow copies that stay safe because every nested member is itself
  immutable.
- Distinguish `ReferenceNumber`'s legitimate absence from `Route`'s
  guarantee that it's never absent, and explain why nullable reference
  types remove the need for a `Route.None`-style Null Object.
- State the CS9035 failure an initializer gets for omitting a required
  member, and explain why that guarantee lives at compile time rather than
  in a runtime check.

## For deeper coverage

- The official Microsoft Learn documentation cited throughout this chapter:
  [C# record types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records),
  [Records - C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record),
  [The `init` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init),
  [The `required` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required),
  [The `with` expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression),
  and [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types).
  This is the primary source for every claim this chapter makes about
  record equality, construction, amendment, and nullability.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
