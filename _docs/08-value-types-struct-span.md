---
title: "Value types: struct, readonly struct, and Span<T>"
order: 8
layout: tutorial
sectionid: docs
part: "dotnet-substrate"
description: "When a value type is the right choice, and the C# 14 implicit span conversions that make Span<T> ergonomic."
---

<!-- EXAMPLE (shipping domain): Money, Weight, and SlotId as structs; Span<T> over an EDI segment. -->

## Learning objectives

By the end of this chapter you can:

- Explain the difference between copy semantics and reference semantics and
  identify when a shipping-domain concept, a money amount, a weight, a slot
  address, is better modeled as a value than as an object with identity.
- Declare a `readonly record struct` that gets value equality, a consistent
  hash code, and an immutable copy-on-assignment with no hand-written
  `Equals`, `GetHashCode`, or `==`/`!=` overloads.
- Name the struct `default`/parameterless-construction pitfall precisely,
  recognize which of a type's fields make a bare default dangerous, and
  supply a type-owned sentinel for the ones that need it.
- State the four hard constraints a `ref struct` carries, stack-only, no
  boxing, no capture across `await` or in a lambda or iterator, and explain
  why each constraint exists.
- Parse a fixed-width record out of a `ReadOnlySpan<char>` with slicing and
  culture-invariant numeric `TryParse` calls, and verify with a measured test
  that the parse allocates zero bytes.

## Why this matters

A rating engine quoting thousands of consignments a minute spends most of
its working set on values with no identity worth tracking: an amount of
money, a weight in kilograms, a warehouse slot address. None of the three
needs a place on the heap, a finalizer, or a notion of "this particular
instance" distinct from any other instance with the same numbers. Two
`Money` values holding 450.75 USD aren't *similar*; they're the *same*
amount, and code that compares them should see that without a hand-written
equality method anywhere in sight.

Modeling that data as a reference type costs more than it returns. Every
consignment record becomes a small heap allocation. A batch of a hundred
thousand records becomes a hundred thousand objects the garbage collector
has to track, and two records holding identical numbers are equal only if
someone remembered to override `Equals`. A value type sidesteps all of it:
equality falls out of the values compared, a copy is just the bits copied,
and nothing ever needs the garbage collector to notice it exists.

The other half of the allocation story shows up where data enters the
system. A carrier hands over a batch of consignment manifests as flat,
fixed-width EDI-style text, one record every twenty-eight characters. A
naive parser reaches for `string.Substring` on each field: six characters
here for a consignment ID, two there for a bay number. Each one becomes a
heap-allocated string, discarded the moment the caller finishes parsing it
into a number. At EDI-feed volumes that habit turns a few hundred thousand
incoming characters into a few hundred thousand transient string
allocations: pure garbage-collector pressure with no value delivered.
Nothing downstream wanted the intermediate strings; it wanted the integers
and decimals they encoded.

`Span<T>` and `ReadOnlySpan<T>` close that gap. A span is a view over memory
that already exists, sliceable without copying and parseable directly, so
the fields inside one record never have to become their own heap objects
just to be read. This chapter works through both halves of the same idea
against the shipping domain. `Money`, `Weight`, and `SlotId` are value
types that get equality and immutability without boilerplate;
`EdiRecordParser` is a span-based parser a measured test proves allocates
nothing in its steady state.

{% include excalidraw.html name="08-value-types-struct-span" caption="Figure 8.1: Value types compare by content instead of reference, and a fixed-width EDI segment is sliced in place into typed fields with no intermediate string allocation." %}

## Copy semantics versus reference semantics

Every type in C# is either a value type or a reference type. The difference
is not a performance footnote: it's a difference in what assignment,
equality, and a method parameter all mean. Assigning a reference-type
variable to another variable copies a reference, so both variables point at
the same object and a mutation through one is visible through the other.
Assigning a value-type variable to another variable copies the value
itself: the two variables hold independent data from that point on, and
nothing one of them does is visible through the other.
`ValueEqualityTests.Value_semantics_mean_assignment_copies_not_aliases`
in the example makes this concrete rather than asserting it as a rule to
take on faith:

```csharp
var original = new Weight(10m);
var copy = original;
copy = copy with { Kilograms = 999m };

Assert.Equal(10m, original.Kilograms);
Assert.Equal(999m, copy.Kilograms);
Assert.NotEqual(original, copy);
```

`copy` starts as an independent bit-for-bit duplicate of `original` the
moment the assignment runs, not a second name for the same storage. The
`with` expression on the next line produces a third value, a modified copy
of `copy`, and reassigns the `copy` variable to hold it. `original` never
changes, because nothing about a `Weight` is shared storage a `with`
expression could reach into.

That's value semantics working the way a reader expects. It's also what
makes a value type safe to hand to an unrelated method, store as a
dictionary key, or pass across a thread boundary without the aliasing bugs
reference types invite. No caller can mutate a `Money` or a `Weight` out
from under another caller holding the same value, because after assignment
the two aren't the same storage at all.

The question this chapter's three shipping types answer isn't "struct or
class" as a performance question. It's "does this concept have an identity
separate from its data." A `Consignment` tracked through a warehouse,
something with a lifecycle, a current location, and a history of state
changes over time, is naturally a reference type. Two consignments with
identical weight and destination are still two different shipments, and
code holding a reference to one should see updates to its tracked state.

`Money`, `Weight`, and `SlotId` have no such lifecycle. A `Money` instance
holding 450.75 USD isn't shipment-specific state that changes over time;
it's a number with a unit, fully described by its two fields. Two
instances with the same two fields are simply the same amount. Microsoft's
own [guidance on choosing between class and
struct](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct)
states the rule directly: "consider defining a struct instead of a class if
instances of the type are small and commonly short-lived or are commonly
embedded in other objects." A value type is the right choice "if it
logically represents a single value, similar to primitive types like int,
double, and so on." `Money`, `Weight`, and `SlotId` all pass that test:
each one logically *is* a single value, not a bundle of state with a
lifecycle of its own.

## readonly record struct: equality without boilerplate

A hand-rolled value type the old way means writing `Equals(object?)`,
`Equals(Money other)` through `IEquatable<Money>`, `GetHashCode`, `==`, and
`!=` by hand, and keeping every one of them consistent with every other
one. Miss a field in `GetHashCode` that `Equals` does check, and a `Money`
can compare equal to another `Money` while hashing to a different bucket.
That's an invariant every dictionary and hash set depends on silently, and
no compiler enforces it for a hand-written override.

The [structure types
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/struct)
flags exactly this cost, recommending a struct override `Equals`,
`GetHashCode`, and the equality operators for value-based comparison.
That's also the fix for [analyzer rule
CA1815](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1815),
"override equals and operator equals on value types." Without those
overrides a struct falls back to `ValueType.Equals`, which compares fields
through reflection. That's slower, and easy to get subtly wrong for a type
with a `string` field like `Money.Currency`, since the default comparison
has to special-case reference fields correctly to behave as expected.

`record struct` closes that gap by generating the overrides the analyzer
asks for, from the declaration alone. `Money`, `Weight`, and `SlotId` are
each declared as a `readonly record struct`:

```csharp
public readonly record struct Weight(decimal Kilograms)       // #A
{
    public decimal Kilograms { get; init; } = Kilograms >= 0m // #B
        ? Kilograms
        : throw new ArgumentOutOfRangeException(nameof(Kilograms), Kilograms, "Weight cannot be negative.");

    public override string ToString() => $"{Kilograms:0.00} kg"; // #C
}
```

`#A` The positional parameter list is also the primary constructor; a
`readonly record struct` from it alone generates `Equals`, `GetHashCode`,
`==`, `!=`, and a default `ToString`.
`#B` The `init` accessor runs the validating expression on every
construction path, so no `Weight` holding a negative value escapes the
type.
`#C` Overriding the generated `ToString` replaces the default record format
with a unit-aware one.

`ValueEqualityTests` proves the generated members behave correctly, not
just that they compile. Two independently constructed `Weight` values
holding the same kilograms are `Equal`, compare `true` through `==`, and
share a hash code. Two differing in kilograms compare unequal through both
`Equals` and `!=`.

The same pattern repeats for `Money` over both of its fields and for
`SlotId` over `Bay` and `Tier`. A fourth type, `ConsignmentRecord`,
composes all three as a `readonly record struct` of its own:
`ConsignmentRecord(int ConsignmentId, SlotId Slot, Weight Weight, Money Charge)`.
It gets a correct recursive equality over every nested value type with the
same zero lines of hand-written comparison code. `record struct`, not a
hand-written `readonly struct` with a manual `IEquatable<T>`
implementation, is the idiomatic choice. The generated members are
mechanically derived from the member list, so they can't drift out of sync
with each other the way independently hand-written overrides can.

The `readonly` modifier on top of `record struct` is a second, separate
guarantee: every instance member not explicitly mutating is treated as
non-mutating, and the compiler rejects any attempt to add one that would
be. `record struct` alone, without `readonly`, still allows mutable
`{ get; set; }` properties. Adding `readonly` on `Money`, `Weight`,
`SlotId`, and `ConsignmentRecord` closes that door explicitly, so a reader
doesn't have to check every property individually to know the type is
immutable. The [`readonly` struct
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct)
notes this also lets the compiler skip defensive copies in some calling
contexts, since it can trust that no member call mutates the instance it's
invoked on.

## The default-value pitfall and the type's own sentinel

Every struct in .NET, record or not, carries an implicit parameterless
state. `default(Money)`, `new Money[3]`, and any `Money` field on a class
that nobody explicitly initializes all produce a `Money` with every field
set to its own bit-pattern zero. None of that path runs through the
validating constructor above. That isn't a bug in this chapter's example;
it's a documented feature the CLR guarantees for every value type. A struct
always has an accessible parameterless constructor that zero-initializes
every field. That holds even when the declared type defines its own
constructors that look like they should be the only way in.

`Money`'s own doc comment names the consequence directly: `default(Money)`
produces `Amount == 0m` and `Currency == null`. `Currency` being `null`
isn't a value `Money.ToString()` or any other member can safely assume,
because the type's own contract promises a three-letter code, validated at
construction, and a bare default never passes through that validation.

The fix isn't to pretend the default state doesn't exist, or to sprinkle
null checks through every member that touches `Currency`. It's to decide,
type by type, whether the zero-initialized default is itself a meaningful
value, and supply a type-owned alternative where it isn't. `Money.Zero` is
that alternative:

```csharp
public static Money Zero { get; } = new(0m, "USD");
```

Code that needs a starting value for an accumulator, or a safe value to
return before a real amount is known, reaches for `Money.Zero`. It runs
through the validating constructor exactly like any other `Money` and
comes out the other side holding a real, formattable currency code.
`ValidationTests.Money_zero_is_a_valid_benign_default` checks exactly that:
`Money.Zero.Amount` is `0m` and `Money.Zero.Currency` is `"USD"`, a value
indistinguishable from one a caller constructed by hand.

`Weight` and `SlotId` need no equivalent sentinel, and their doc comments
say so explicitly rather than leaving a reader to wonder why only one of
the three types has a `Zero` property. `default(Weight)` is
`Kilograms == 0m`, a real, legitimate weight of nothing. `default(SlotId)`
is `Bay == 0, Tier == 0`, a real, addressable slot at the first bay and
first tier. Neither default is unsafe to read or format, so adding a
sentinel for either one would be ceremony without a corresponding bug to
prevent.

The lesson generalizes: before adding a `Zero`, `Empty`, or `None` static
property to a value type, check whether the zero-initialized default is
actually dangerous for that type's invariants, the way a `null` string
field is. Don't add the sentinel reflexively regardless of whether the
default needs one.

## ref struct: a type that cannot leave the stack

`EdiRecordParser` carries a `ReadOnlySpan<char>` as a field, and that
single fact forces it into a different category of struct than `Money`,
`Weight`, or `SlotId`. A span is itself a stack-only type: internally it
holds a reference to the start of a block of memory together with a
length. That reference may point partway into an array, into a string's
internal buffer, or onto the stack itself via `stackalloc`. If a span
field could be copied onto the heap, boxed into an `object`, or kept alive
past the lifetime of whatever memory it refers to, a later read through it
would read freed or relocated memory. That's the exact bug the span exists
to prevent.

The CLR's rule for `ref struct`, the category `EdiRecordParser` belongs to,
makes that bug impossible to write rather than merely unlikely: a
`ref struct` can only ever live on the stack.

```csharp
public readonly ref struct EdiRecordParser
{
    private readonly ReadOnlySpan<char> _segment;              // #A

    private EdiRecordParser(ReadOnlySpan<char> segment) => _segment = segment; // #B

    public static bool TryParse(ReadOnlySpan<char> segment, out ConsignmentRecord record) // #C
    {
        var parser = new EdiRecordParser(segment);
        return parser.TryParseCore(out record);
    }
}
```

`#A` The `ref struct` modifier on the type and the `ReadOnlySpan<char>`
field together are what the CLR's stack-only rule applies to.
`#B` The constructor is `private`, so every `EdiRecordParser` traces back
to a `TryParse` call with its own fresh span.
`#C` `TryParse` is the only public entry point, and it returns before any
span could outlive the call.

The [`ref struct` type
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct)
lists the restrictions this stack-only guarantee implies, and every one of
them ties back to the same aliasing concern:

- A `ref struct` can't be boxed: boxing moves a value onto the heap, and a
  heap object can outlive the stack frame whose memory a span inside it
  might reference.
- It can't be a field of a class or a non-`ref` struct, for the same
  reason: a class instance lives on the heap, and any field inside it
  inherits the heap's lifetime.
- It can't be used as a type argument, captured by a lambda or local
  function, or stored in a heap collection: capturing a variable lifts it
  into a compiler-generated class, and that lift is the heap allocation
  the rules forbid.
- It can't cross an `await` boundary or live as a field inside an
  iterator's state machine: both compile down to a heap-allocated object
  carrying local variables across suspension points, where a `ref struct`
  field would again end up living on the heap.

`EdiRecordParser`'s own `TryParse` is fully synchronous, calls only itself,
and never hands its span to a lambda, exactly the shape the type requires.
None of these restrictions is arbitrary friction: each one closes a
specific path by which a stack-only reference could outlive the stack
frame it points into.

## Span<T> and ReadOnlySpan<T>: a view, not a copy

A `Span<T>` or `ReadOnlySpan<T>` is a window onto memory that already
exists somewhere else: an array, a string's backing buffer, or
stack-allocated memory, described by a starting reference and a length and
nothing more. The [`Span<T>`
documentation](https://learn.microsoft.com/en-us/dotnet/api/system.span-1)
calls it "an allocation-free representation of contiguous regions of
arbitrary memory," and that allocation-free property is the whole value
proposition against the shipping domain's EDI feed.

`EdiRecordParser` receives its input as a `ReadOnlySpan<char>` and never
widens it into a `string` on the happy path. `TryParseCore` slices the one
incoming span into six smaller spans, one per field, each a view into the
same underlying character buffer at a different offset and length:

```csharp
if (!int.TryParse(_segment.Slice(ConsignmentIdOffset, ConsignmentIdLength), NumberStyles.None, CultureInfo.InvariantCulture, out var consignmentId)) // #A
{
    return false;
}

if (!decimal.TryParse(_segment.Slice(WeightOffset, WeightLength), NumberStyles.Number, CultureInfo.InvariantCulture, out var kilograms)) // #B
{
    return false;
}
```

`#A` `Slice` returns a view into `_segment`, not a copy; `NumberStyles.None`
rejects a sign or decimal point the zero-padded integer field should never
carry.
`#B` `NumberStyles.Number` allows the decimal point the two-decimal amount
field needs; both calls pass `CultureInfo.InvariantCulture` explicitly,
since the wire format doesn't vary with the machine's regional settings.

Every numeric `TryParse` overload used here accepts a `ReadOnlySpan<char>`
directly, so the digits never become their own `string` before
`int.TryParse` or `decimal.TryParse` reads them. The one field that ends
up as a `string`, the currency code, still allocates nothing.
`TryParseCurrency` compares the three-character slice against each
supported code with `ReadOnlySpan<char>.SequenceEqual`, and on a match
returns the matching string *literal*, already interned by the runtime
rather than built fresh. Even the single `string`-typed field in the
result costs nothing beyond a handful of character comparisons.

{% include excalidraw.html name="08-fixed-width-edi-layout" caption="Figure 8.2: A 28-character EDI segment is sliced at fixed offsets directly into typed fields, assembling one consignment record without allocating an intermediate string." %}

`int.TryParse` and `decimal.TryParse` over `ReadOnlySpan<char>` aren't a
workaround bolted on for this one parser. They're the current idiomatic
entry point for culture-aware numeric parsing in .NET, available alongside
the long-standing `string`-based overloads so span-based code never has to
materialize a string just to call them.

C# 14 goes a step further by making the span and array worlds
interchangeable at more call sites. The language now allows an implicit
conversion from an array or certain other collection types to `Span<T>` or
`ReadOnlySpan<T>` in more positions than earlier versions permitted. The
[C# 14 implicit span conversions
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions)
describes the change, which removes some of the explicit `.AsSpan()` calls
earlier C# versions needed at a method boundary. `EdiRecordParserTests`
leans on exactly that
improvement: its test fixtures build segments as plain `string` values and
hand them straight to `EdiRecordParser.TryParse(ReadOnlySpan<char>
segment, ...)` with no explicit conversion at the call site, because a
`string` converts to `ReadOnlySpan<char>` without ceremony.

The malformed-input tests make `TryParse`'s contract trustworthy, not
merely documented. `TryParse_segment_too_short_returns_false_and_does_not_throw`,
its too-long counterpart, a non-numeric `ConsignmentId`, a non-numeric
weight field, and an unrecognized currency code each produce `false` and a
`default` `ConsignmentRecord` out parameter, never an exception. That
matters because a carrier feed with one malformed record among a hundred
thousand well-formed ones shouldn't take down the whole batch; the caller
checks the boolean and decides what to do with the bad row.

The measured proof that this costs nothing on the heap lives in
`NoAllocationTests.TryParse_steady_state_allocates_nothing_on_the_current_thread`,
covered next: a hundred thousand parses in a row move the needle on
`GC.GetAllocatedBytesForCurrentThread()` by exactly zero bytes.

## Build, run, observe

This chapter makes two claims: the generated value equality on `Money`,
`Weight`, `SlotId`, and `ConsignmentRecord` is correct, and
`EdiRecordParser`'s steady-state span parsing allocates zero bytes, a
measured number, not an estimate. The suite below proves both. Every
command runs with the local .NET 10 SDK; there's no container anywhere in
this loop. The example is standalone-testable from its own directory.

```sh
cd examples/08-value-types-struct-span
dotnet test
```

It's also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK.

```
Test run summary: Passed!
  total: 29
  failed: 0
  succeeded: 29
  skipped: 0
  duration: 1s 892ms
```

The 29 cases split across four files, each tied to one of this chapter's
claims.

- **`ValueEqualityTests` proves the generated equality is correct, not
  merely present.** Independently constructed values with identical
  fields on `Money`, `Weight`, `SlotId`, and `ConsignmentRecord` are equal
  and share a hash code; values differing in any one field compare
  unequal; a `with`-derived copy leaves the original untouched.
- **`ValidationTests` proves the throwing constructors reject nonsensical
  values.** Negative money, a malformed currency code, negative weight,
  and negative bay or tier each throw, and `Money.Zero` is itself checked
  as a valid, non-throwing value rather than another trap.
- **`EdiRecordParserTests` proves `TryParse`'s contract end to end.** A
  known-good segment lands every field in its correct typed slot and
  recognizes every supported currency code; a too-short segment, a
  too-long segment, a non-numeric field, an unrecognized currency code,
  and an empty span each return `false` without throwing.
- **`NoAllocationTests` is the measured proof of the chapter's headline
  claim.** It warms up the JIT and any one-time static initialization
  with ten thousand throwaway parses, forces a full garbage collection,
  records `GC.GetAllocatedBytesForCurrentThread()`, runs a hundred
  thousand more parses, records the counter again, and asserts the two
  readings are identical. The steady-state parse allocates zero bytes,
  not "close to zero" or "fewer than before."

## When not to use it

Value types earn their keep when a type is small, logically immutable, and
has no identity beyond its own data. None of those properties survives
automatically once a struct grows. A struct with a dozen fields pays a
real copying cost on every assignment, every method parameter, and every
array element access, a cost a reference type, copied as a single
pointer-sized reference, doesn't pay. `Money`, `Weight`, and `SlotId` stay
small enough, one or two scalar fields each, that the copy is cheap enough
not to matter. A hypothetical shipment-manifest struct carrying twenty
fields would be the wrong shape for a value type regardless of whether its
data has no identity. The copying cost would start competing with the
allocation cost a struct is supposed to avoid.

Reference identity is the other disqualifier. A type whose instances need
to be told apart even when their data matches, a tracked `Consignment`
moving through a warehouse with a lifecycle and a mutable current state,
is a reference type by nature. Forcing it into a struct to chase the same
allocation savings means giving up the identity semantics the domain
concept needs. Inheritance is a third: a struct can't be a base class or
derive from another struct, only implement interfaces. A concept that
needs a type hierarchy, several carrier implementations sharing a base
class's behavior rather than an interface's contract, isn't a candidate no
matter how small its data looks.

`ref struct` carries its own sharper boundary: reach for one only when a
type's entire job is synchronous, non-capturing, and finished before the
method that created it returns. `EdiRecordParser` fits that shape exactly:
parse one segment, return a result, done, with no span ever asked to
survive past the call. A parser that needs to yield records lazily through
an iterator, or hand a partially parsed span off to an asynchronous
downstream step, can't be a `ref struct` at all. The compiler rejects
exactly the iterator state machine or `await` boundary that shape
requires. The fix there isn't to fight the restriction but to materialize
the needed data, a `ConsignmentRecord` or a `string`, before crossing that
boundary, and keep the `ref struct` itself confined to the fully
synchronous slice.

A span passed into a method and stored in a field that outlives the call
is a compile error for a `ref struct` field. For any other value
smuggling a span out some other way, it's a latent bug waiting to happen.
The compiler catches the `ref struct` case directly. But the discipline of
not letting a span escape its stack frame has to hold even where the
compiler can't enforce it, such as a span returned from a method and then
held past the lifetime of its backing memory.

<blockquote class="ai-sidebar" markdown="1">
Asked to "make this a struct for performance," a generated answer often
flips the keyword without checking the disqualifiers above. It leaves
mutable `{ get; set; }` auto-properties instead of reaching for
`readonly record struct`, or stores a `Span<T>` field somewhere it can
outlive the call. Put this chapter's decision rules in the prompt:

> Convert this class to a value type only if it's small, has no identity
> beyond its data, and needs no inheritance; use `readonly record struct`,
> not a hand-written struct with mutable properties. If you add a
> `Span<T>` or `ReadOnlySpan<T>` field, confine it to a `ref struct` whose
> only job is one synchronous, non-capturing call, and never let it
> outlive that call.

If the reply leaves a mutable property, or stores a span somewhere the
compiler wouldn't reject on a declared `ref struct`, ask for the
`readonly record struct` or `ref struct` form instead.
</blockquote>

## Where this leaves you

You can now:

- Explain the difference between copy semantics and reference semantics,
  and point to `Weight`'s `with`-expression test as proof that an
  assignment copies rather than aliases.
- Declare a `readonly record struct` like `Money`, `Weight`, or `SlotId`
  and get value equality, a consistent hash code, and immutability with no
  hand-written `Equals`, `GetHashCode`, or `==`/`!=` overrides.
- Name the default/parameterless-construction pitfall, recognize that
  `Money.Currency` needs a guard against it, and supply `Money.Zero` as the
  type-owned sentinel, while explaining why `Weight` and `SlotId` need
  none.
- State the four `ref struct` constraints, no boxing, no field on a class,
  no type argument or capture, no crossing `await`, and connect each one
  to the heap-escape it closes off, the way `EdiRecordParser` does.
- Parse a fixed-width record out of a `ReadOnlySpan<char>` with `Slice` and
  culture-invariant `TryParse`, and verify with `NoAllocationTests` that
  the steady-state parse allocates zero bytes.

## For deeper coverage

- The official Microsoft Learn documentation cited throughout this chapter:
  [Structure types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/struct),
  [`readonly` struct](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct),
  [`ref struct` types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct),
  [`Span<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.span-1) and
  [`ReadOnlySpan<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1),
  [choosing between class and struct](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct),
  [CA1815: override equals and operator equals on value types](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1815),
  and the [C# 14 implicit span conversions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions)
  this chapter's example relies on at its test call sites.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
