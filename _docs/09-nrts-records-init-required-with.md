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

- Explain what compiler-generated equality a record class gives for free, and
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

A consignment is not a fact fixed at the moment of booking. A customs broker
corrects a declared weight after a re-weigh at the depot. A dispatcher
re-routes a shipment around a closed border crossing. A customer calls back
two days later with a purchase-order number to attach for their own records.
None of these are new consignments; they are amendments to one that already
exists, and a system that represents a consignment as a plain mutable class
handed around by reference invites exactly the bug class that mutation always
invites: two parts of the system holding what they each believe is "the"
consignment, one of them calls a setter to correct the weight, and the other
silently sees the change too, whether or not it was ready for it, because
there was only ever one object and every reference to it pointed at the same
mutable state. A rating engine that captured a weight for billing purposes
before a correction lands has no record of what it actually billed against,
because the object it held a reference to changed out from under it.

Modeling `Consignment` as an immutable value instead removes that failure
mode structurally rather than procedurally. Booking a consignment produces
one value. Correcting the weight does not mutate that value; it produces a
second, independent value that differs from the first in exactly the field
that changed, while the first value, and everyone still holding a reference
to it, is completely unaffected. A re-route produces a third value. Nothing
downstream has to guess whether the object it is looking at might change
underneath it, because it cannot: the only way to get a different
`Consignment` is to ask for one explicitly, by name, through an operation
that returns a new value. That guarantee does not come from documentation or
convention; it comes from the record declaration itself, and this chapter's
second half shows why.

The other half of the picture concerns what a `Consignment` is permitted to
hold. A `ConsignmentId` and a `Route` must always be present; a shipment with
no route literally has nowhere to go, and treating that as a possibility
every downstream method has to handle defensively, a `null`-checked `Route`
property, is pure accidental complexity. A `ReferenceNumber`, on the other
hand, is sometimes genuinely absent, a customer-supplied purchase-order code
that may never arrive at all. Before nullable reference types, both of those
facts had to be remembered by convention and enforced, if at all, by a hand
written constructor and scattered documentation comments; the type system
itself could not tell a reader, or the compiler, which properties were safe
to dereference without a check and which ones were not. Nullable reference
types turn that distinction into part of the type itself, and `required`
closes the one gap nullability alone cannot close: a property the compiler
knows can never be `null` but that nothing actually forces a caller to set.
Put together, `required`, `init`, `with`, and nullable reference types let
`Consignment` state its own contract once, at the declaration, and have the
compiler hold every caller to it.

{% include excalidraw.html name="09-nrts-records-init-required-with" caption="Figure 9.1: TODO caption." %}

## Records and recursive value equality

`Consignment` is a `sealed record`, which makes it a reference type, not a
value type: assigning one `Consignment` variable to another copies a
reference, the same as assigning any class. What a record adds on top of
that reference-type foundation is compiler-generated structural equality.
Unlike `Route` in the previous section's mention, `Consignment` is declared
without a primary constructor, every member spelled out as its own property:

```csharp
public sealed record Consignment
{
    public required string ConsignmentId { get; init; }
    public required Route Route { get; init; }
    public required Weight Weight { get; init; }
    public string? ReferenceNumber { get; init; }
    public Money DeclaredValue { get; init; } = Money.Zero;
}
```

That non-positional shape is a deliberate choice: a record with a primary
constructor reads naturally for a handful of short positional members, the
way `Route`'s `Origin` and `Destination` do, but a five-member type with two
different enforcement rules per member, three `required`, one nullable, one
defaulted, reads more clearly as named properties than as five positional
parameters a caller has to get in order. The `record` keyword does the same
work regardless of which shape is chosen: it synthesizes `Equals(Consignment?
other)`, the `IEquatable<Consignment>` implementation that backs it,
`GetHashCode`, `operator ==`, `operator !=`, and a copy constructor, all
derived from the declared properties. `ValueEqualityTests` proves the
generated members are correct rather than merely present:
`Two_independently_constructed_consignments_with_identical_data_are_equal`
builds two separate `Consignment` instances from the same field values and
asserts `Equals`, `==`, and a shared `GetHashCode()` all agree that the two
are the same consignment, despite being two different objects on the heap
that happen to share no reference at all.

The generated equality does not stop at `Consignment`'s own five properties;
it recurses into every member that itself knows how to compare itself.
`Route` is `public sealed record Route(string Origin, string Destination)`,
a positional record with its own compiler-generated equality over its two
strings, and `Weight` and `Money` are each a `readonly record struct` with
equality over their own fields. When `Consignment.Equals` compares two
`Route` values, it calls `Route`'s own `Equals`, which compares `Origin` and
`Destination` by value, not by reference; when it compares two `Weight`
values, it calls `Weight`'s own value-equality over `Kilograms`.
`Changing_the_nested_route_makes_consignments_unequal` and
`Changing_the_weight_makes_consignments_unequal` both confirm this: changing
only the nested `Route.Destination`, or only the nested `Weight.Kilograms`,
on an otherwise identical consignment is enough to make the two compare
unequal, because the comparison is genuinely structural all the way down,
not merely a comparison of `Consignment`'s own top-level fields.

That recursion carries a caveat worth stating precisely rather than leaving
implicit. A record's generated equality calls `Equals` on each member, but it
cannot upgrade a member's own equality semantics; it can only use whatever
`Equals` that member's type actually provides. `Route`, `Weight`, and `Money`
all happen to be records themselves, so the recursion stays structural the
whole way down in this example. A hypothetical `CustomsDeclaration` class
with no `record` modifier and no hand-written `Equals` override would fall
back to the default reference equality every plain class gets from `object`,
and `Consignment`'s generated equality would compare two `CustomsDeclaration`
references, not their contents; two consignments holding separately
constructed but field-identical declarations would then compare unequal
despite looking, to a reader, exactly alike. The fix is not a special case in
`Consignment`'s equality; it is choosing a record, or a type with its own
correct value equality, for every member that needs to participate in
structural comparison. [The C# record types
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records)
states the underlying rule directly: a record's generated equality compares
"the values of all the fields and properties of the object," and that
comparison is only as deep as the equality each individual field or property
type provides.

## required and init: closing the construction gap

Before `required` existed, a non-nullable property on a type with no
constructor forcing its assignment left a reader, and the compiler, in an
uncomfortable position. Nullable reference types will flag a non-nullable
property that a constructor never assigns with a "possibly null" warning
when the compiler cannot prove every code path sets it, but a warning is not
an error: a developer can see it, decide it's noise, and ship anyway, and the
property can still end up holding `null` at runtime despite its declared
type saying it never would. The usual fix before C# 11 was a hand-written
constructor that took every non-nullable member as a parameter and assigned
it, trading a compile warning for actual enforcement, at the cost of a
constructor that has to be kept in sync by hand every time a property is
added or removed.

`required` removes that trade entirely. A property marked `required` must be
set by every object initializer that constructs the type, and the compiler
enforces this as a hard error, not a suppressible warning. `ConsignmentId`,
`Route`, and `Weight` are each declared `public required string ConsignmentId
{ get; init; }` and its siblings, with no constructor anywhere in
`Consignment` doing the assigning instead. Leaving one out does not compile:

```csharp
// CS9035: Required member 'Consignment.Route' must be set.
var broken = new Consignment
{
    ConsignmentId = "CNS-4217",
    Weight = new Weight(1250m),
};
```

That failure happens during compilation, before the program containing it
can run at all, which is exactly why this example has no test asserting it:
a test can only exercise code that compiles. `RequiredMembersTests` instead
proves the positive path, that an initializer setting every required member
compiles cleanly and produces a `Consignment` whose properties hold exactly
the values supplied, with `ReferenceNumber` defaulting to `null` and
`DeclaredValue` defaulting to `Money.Zero` for the two members the
initializer leaves unset.

`init` is the other half of the pairing, and it answers a different
question: not whether a value must be supplied, but whether it can ever
change after construction. An `init` accessor, [the documentation
explains](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init),
"assigns a value to the property or the indexer element only during object
construction," through either a constructor or an object initializer; any
attempt to assign to it afterward, `consignment.Route = newRoute` from
outside the type, is a compile error. Every property on `Consignment` uses
`init` rather than `set`, which is what makes the type's immutability a
compile-time guarantee rather than a convention a reader has to trust:
nothing in the codebase, including code written after this chapter, can
reach into an existing `Consignment` and change a field in place, because
the language gives it no accessor capable of doing so.

`required` and a default value are not in tension with each other, and
`DeclaredValue` demonstrates exactly that: `public Money DeclaredValue {
get; init; } = Money.Zero;` is non-nullable, like `Route` and `Weight`, but
not `required`, because it already has a value, `Money.Zero`, that satisfies
the "always has a sensible value" contract without needing a caller to
supply one explicitly. An initializer that omits `DeclaredValue` compiles
fine and produces a consignment with a declared value of zero euros, a
legitimate value for a consignment nobody has valued yet, not a stand-in for
absence. The distinction `required` draws is not "nullable versus
non-nullable"; it is "has a type-owned default versus has no sensible value
until a caller supplies one." `ConsignmentId` has no default string that
would mean anything; a blank or placeholder identifier would be actively
wrong, which is exactly the case `required` exists to force a caller to
settle at construction.

## Non-destructive amendment with with

`Consignment` exposes three amendment methods, each one a single-expression
wrapper around a `with` expression that returns a new value and leaves the
receiver untouched:

```csharp
public Consignment ReRouteTo(string destination) =>
    this with { Route = Route with { Destination = destination } };

public Consignment WithCorrectedWeight(Weight weight) =>
    this with { Weight = weight };

public Consignment WithReferenceNumber(string referenceNumber) =>
    this with { ReferenceNumber = referenceNumber };
```

`WithAmendmentTests` proves what each one actually does rather than taking
"returns a new value" on faith.
`ReRouteTo_returns_a_new_consignment_and_leaves_the_original_unchanged`
re-routes a consignment from Hamburg to Antwerp and then checks both values:
the amended consignment's `Route.Destination` reads `"Antwerp"`, and the
original's still reads `"Hamburg"`, confirming the original was never
touched. `WithCorrectedWeight` and `WithReferenceNumber` each get the same
treatment, and a fifth test,
`Declared_value_can_be_amended_with_a_bare_with_expression_at_the_call_site`,
amends `DeclaredValue` directly from the test, `original with { DeclaredValue
= new Money(18_500m, "EUR") }`, with no dedicated helper method at all,
proving that a type does not have to wrap every conceivable amendment in a
named method for `with` to stay useful; a one-off amendment at a call site
works exactly the same way a named helper does, because both desugar to the
same copy-and-override operation.

`ReRouteTo` nests one level deep, and that nesting is worth looking at
closely, because it is where `with`'s actual mechanics show through. `this
with { Route = Route with { Destination = destination } }` evaluates the
inner `with` expression first: `Route with { Destination = destination }`
produces a brand-new `Route` value, a copy of the current `Route` with only
`Destination` overridden, leaving the current `Route`'s own `Origin`
untouched. That new `Route` is then handed to the outer `with` expression as
the replacement value for `Consignment.Route`. Both `with` expressions are
doing the same thing at different levels: take the receiver, produce a
member-by-member copy, override the named members, return the result. [The
`with` expression
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression)
describes this as calling a compiler-synthesized copy constructor on the
receiver and then applying the specified property assignments to the copy,
which is exactly the two-step shape both levels of `ReRouteTo` follow.

The documentation is explicit about one more point that matters here: "the
result of a `with` expression is a shallow copy," which means a reference
typed member is copied as a reference, not as a deep clone of whatever it
points to. That is precisely why `Consignment`'s design routes every nested
member through its own record or record struct rather than a plain mutable
class. `Route` being an immutable record means the shallow copy a `with`
expression produces is completely safe: the outer `with` copies the
reference to a `Route` value, but that value can never change out from
under either the original or the amended consignment, because `Route` has no
setter anywhere on it. If `Route` were instead a mutable class with ordinary
`get; set;` properties, the outer `with`'s shallow copy would leave both the
original `Consignment` and the newly amended one pointing at the very same
`Route` object; mutating that shared `Route` through either consignment's
reference would silently change what the other one reports too, reproducing
exactly the aliasing bug immutability was supposed to remove. Modeling
`Route`, `Weight`, and `Money` as records and record structs is not a style
preference separate from the `with`-based amendment design; it is what makes
that design sound. `An_amendment_that_reproduces_the_original_data_compares_equal_to_it`
closes the loop on this: re-routing a consignment back to the destination it
started at produces a value that compares equal to the original through
`==`, even though it is a distinct object, because equality here is
genuinely about the data, never about which particular instance produced it.

## Nullable reference types: absence versus never

`ReferenceNumber` is declared `string?`, and that single character carries
real meaning: a newly booked consignment has no purchase-order reference yet,
and that is not an error condition to work around, it is an expected,
ordinary state. `NullabilityTests.ReferenceNumber_is_null_on_a_freshly_constructed_consignment`
confirms exactly that: a `Consignment` built without a `ReferenceNumber`
compiles fine, because the property is nullable rather than `required`, and
its value reads `null` until a customer supplies a reference number through
`WithReferenceNumber`, which
`ReferenceNumber_is_non_null_after_an_amendment_that_adds_it` confirms
produces a consignment whose `ReferenceNumber` now holds the supplied value.
[The nullable reference types
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types)
frames the feature as annotations the compiler uses "to help you find
potential null reference errors," and the annotation here is doing exactly
the job it exists for: telling every caller, through the type itself, that
checking `ReferenceNumber` for `null` before using it is not defensive
paranoia, it is the type's actual contract.

Every other member on `Consignment` draws the opposite line.
`ConsignmentId`, `Route`, and `Weight` are declared without the `?`, which
tells both the compiler and a reader that a legitimate `Consignment` never
holds `null` in any of them, and `required` is what turns that declared
intent into an enforced guarantee rather than a hopeful assertion.
`Required_non_nullable_members_never_turn_null_across_a_with_expression`
chains two amendments, a re-route followed by a weight correction, and
confirms `ConsignmentId` and `Route` are still non-null and still correct
afterward; nothing about a `with` expression, nested or not, has any path
that could leave a non-nullable, `required` member holding `null`, because
every `with` expression starts from a fully valid `Consignment` and only
ever overrides the members it names.

This is the pairing that matters more than either half alone: nullable
reference types describe which members are allowed to be absent, and
`required` guarantees every member that is not allowed to be absent actually
has a value the moment construction finishes. Before both existed together,
a team without them reached for a different fix for the same underlying
problem, the Null Object pattern: a type like `Route` would ship with a
static `Route.None` or `Route.Empty` sentinel standing in for "no route yet,"
so that code calling `route.Origin` never had to null-check first, because
the sentinel supplied harmless placeholder values instead of `null`. That
pattern earns its cost specifically when a language cannot distinguish
"guaranteed present" from "might be absent" in the type system itself, so a
sentinel has to carry the weight a type annotation can't. With nullable
reference types and `required` together, the distinction *is* in the type
system: `Route` on `Consignment` is `required`, so it is simply never
missing, full stop, and no sentinel is needed to stand in for a state the
type cannot be in; `ReferenceNumber` is genuinely sometimes missing, so it is
`string?`, and the honest way to represent that is `null`, checked once at
the point of use, not a placeholder object pretending to be a reference
number that was never supplied. The reflex to reach for a Null Object loses
its motivation once the type system can say "this is required" and "this is
optional" directly and have the compiler hold every caller to whichever one
is true.

## Build, run, observe

Every command below runs with the local .NET 10 SDK; there is no container
anywhere in this loop. The example is standalone-testable from its own
directory:

```sh
cd examples/09-nrts-records-init-required-with
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 15
  failed: 0
  succeeded: 15
  skipped: 0
  duration: 911ms
```

The 15 cases split across four files, each proving a different claim this
chapter made. `ValueEqualityTests` proves the generated equality on
`Consignment`, `Route`, `Weight`, and `Money` is correct, not merely present:
independently constructed values with identical data are equal and share a
hash code, and changing either the nested `Route` or the nested `Weight`
alone is enough to make two otherwise-identical consignments compare
unequal, confirming equality genuinely recurses rather than stopping at
`Consignment`'s own top-level fields. `WithAmendmentTests` proves every
amendment helper, and the bare `with` expression amending `DeclaredValue` at
the test's own call site, returns a new value while leaving the original
completely unchanged, and that re-routing a consignment back to its starting
destination produces a value equal to the one it started from. `NullabilityTests`
proves `ReferenceNumber` reads `null` on a freshly built consignment and
non-null once an amendment supplies one, and that the required, non-nullable
members never turn null across a chain of `with`-based amendments.
`RequiredMembersTests` proves an object initializer that sets every required
member compiles and constructs a `Consignment` whose two optional members,
`ReferenceNumber` and `DeclaredValue`, correctly fall back to `null` and
`Money.Zero`.

## When not to use it

Records, `init`-only immutability, and `with`-based amendment all earn their
keep when a type's job is to represent a value: something two instances can
be genuinely the same by virtue of holding the same data, with no further
question of "which one." That is not every type a shipping system needs.
A consignment tracked live through a warehouse management system, one with a
database-assigned identity, a current physical location, and a change
history an operator can audit, needs identity semantics a record actively
works against: two tracking rows describing the same physical shipment at
two different moments would have different field values and would therefore
compare unequal under a record's structural equality, even though they are
"the same consignment" in every sense that actually matters to that system.
An entity-tracking layer such as an ORM's change tracker also depends on
holding one canonical, mutable instance per tracked row and noticing when
its properties change; a type that hands back a new, independent value on
every amendment has nothing for a change tracker to watch, because nothing
ever mutates in place for it to observe. Identity-bearing types like that
belong on a class compared by a stable key, not on a record compared by
every field it carries.

The shallow-copy behavior of `with` is a second boundary worth naming
explicitly, because it does not announce itself as a problem until a nested
member happens to be mutable. This chapter's design works specifically
because `Route`, `Weight`, and `Money` are themselves immutable, so sharing
a reference to one across an original and its amended copy is harmless. A
larger object graph that mixes immutable records with ordinary mutable
classes somewhere inside it loses that safety the moment a `with` expression
copies a reference to one of the mutable parts: the "amended copy" and the
original end up pointing at the same mutable object for every member the
`with` expression didn't explicitly override, and a later mutation through
either path leaks into both, silently defeating the whole point of
non-destructive amendment. The fix is not avoiding `with`; it is keeping
every member that participates in it immutable too, the same discipline
`Consignment` already follows, rather than reaching for a record around a
mix of mutable and immutable parts and trusting the shallow copy to behave
like a deep one.

A third boundary is pure cost under sustained pressure. Each `with`
expression allocates a new `Consignment` on the heap; a batch correction
replaying ten thousand historical weight adjustments through
`WithCorrectedWeight` in a tight loop allocates ten thousand objects, almost
all of them garbage the instant the next one replaces it as "the current
value" in the loop. For a consignment amended a handful of times over its
whole lifetime, re-routed once, corrected once, a reference number attached
once, that allocation cost is immaterial against the bugs it prevents. For a
genuinely hot path reprocessing the same value many times a second, a
carefully encapsulated mutable type, with setters kept private and changes
routed through methods that preserve the type's invariants, can be the
better trade; immutability is a design default here, not an unconditional
rule that every value-shaped type must follow regardless of how often it
changes.

> **AI sidebar.** Ask an AI pairing partner to "fix the nullable warning" on
> a non-nullable property a constructor doesn't assign, and a common
> response reaches for the easy fix rather than the correct one: adding a
> `?` to the property's type, or initializing it to `null!`, both of which
> silence the warning without actually guaranteeing the property ever holds
> a meaningful value. Neither is what `required` is for, and a generated
> suggestion that proposes either one is solving the compiler's complaint
> rather than the underlying problem, which is that the type never actually
> forces a caller to supply the value. A second, related habit shows up
> around optional data: asked to model something that's "sometimes missing,"
> a generated answer will sometimes reach for a sentinel object, an `Empty`
> or `None` static instance standing in for absence, out of old habit from
> languages or codebases where nullable annotations didn't exist to do that
> job directly. On a record with nullable reference types already turned on,
> that sentinel is solving a problem the type system already solves: `string?`
> plus a `null` check at the point of use says exactly the same thing with
> less code and no extra object to construct, compare, or special-case.
> Check a generated fix against what this chapter actually distinguishes: is
> the member genuinely optional, in which case make it nullable and check
> it, or does it need to be guaranteed present, in which case `required` is
> the fix, never a suppressed warning or a `null!` that only moves the
> failure from compile time to a runtime surprise.

## For deeper coverage

- The official Microsoft Learn documentation cited throughout this chapter:
  [C# record types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records),
  [Records - C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record),
  [The `init` keyword](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init),
  [The `required` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required),
  [The `with` expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression),
  and [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types),
  the primary source for every claim this chapter makes about record
  equality, construction, amendment, and nullability.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
