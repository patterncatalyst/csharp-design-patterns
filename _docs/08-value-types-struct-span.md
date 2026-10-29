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
its working set on values that have no identity worth tracking: an amount of
money, a weight in kilograms, a warehouse slot address. None of these three
needs a place on the heap, a finalizer, or a notion of "this particular
instance" distinct from any other instance with the same numbers. Two
`Money` values holding 450.75 USD are not *similar*, they are the *same*
amount, and code that compares them should see that without a hand-written
equality method anywhere in sight. Modeling that kind of data as a reference
type costs more than it returns: every consignment record becomes a small
heap allocation, every batch of a hundred thousand records becomes a
hundred thousand objects the garbage collector has to track, and two records
holding identical numbers are only equal if someone remembered to override
`Equals`. A value type sidesteps all of it. Equality falls out of the
values compared, a copy is just the bits copied, and nothing ever needs the
garbage collector to notice it exists.

The other half of the allocation story shows up at the boundary where data
enters the system. A carrier hands a batch of consignment manifests over as
flat, fixed-width EDI-style text, one record every twenty-eight characters,
and a naive parser reaches for `string.Substring` on each field: six
characters here for a consignment ID, two there for a bay number, and a
heap-allocated string for every one of them, discarded the moment the
caller finishes parsing it into a number. At EDI-feed volumes that habit
turns a few hundred thousand incoming characters into a few hundred
thousand transient string allocations, pure garbage-collector pressure with
no value delivered, because nothing downstream ever wanted the intermediate
strings in the first place; it wanted the integers and decimals they
encoded. `Span<T>` and `ReadOnlySpan<T>` exist to close that gap: a span is
a view over memory that already exists, sliceable without copying and
parseable directly, so the fields inside one record never have to become
their own heap objects just to be read. This chapter works through both
halves of the same idea against the shipping domain: `Money`, `Weight`, and
`SlotId` as value types that get equality and immutability for free, and
`EdiRecordParser`, a span-based parser proven by a measured test to
allocate nothing at all in its steady state.

{% include excalidraw.html name="08-value-types-struct-span" caption="Figure 8.1: Two readonly record struct Money values compared by content, set against a reference-type class for contrast; a ReadOnlySpan<char> over one fixed-width EDI segment sliced in place into ConsignmentId, SlotId, Weight, and Money, with no intermediate string allocation." %}

## Copy semantics versus reference semantics

Every type in C# is either a value type or a reference type, and the
difference is not a performance footnote, it is a difference in what
assignment, equality, and a method parameter all mean. Assigning a
reference-type variable to another variable copies a reference: both
variables now point at the same object, and a mutation through one is
visible through the other. Assigning a value-type variable to another
variable copies the value itself: the two variables hold independent data
from that point on, and nothing one of them does is visible through the
other. `ValueEqualityTests.Value_semantics_mean_assignment_copies_not_aliases`
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
`with` expression on the following line produces a third value, a modified
copy of `copy`, and reassigns the `copy` variable to hold it; `original`
never changes, because nothing about a `Weight` is shared storage that a
`with` expression could reach into. That is value semantics working exactly
as a reader expects it to, and it is also the property that makes a value
type safe to hand to an unrelated method, store in a dictionary key, or pass
across a thread boundary without the aliasing bugs that reference types
invite: no caller can mutate a `Money` or a `Weight` out from under another
caller holding what looks like the same value, because after assignment
they are not the same storage at all.

The question this chapter's three shipping types answer is not "struct or
class," phrased as a performance question, but "does this concept have an
identity separate from its data." A `Consignment` as tracked through a
warehouse, something with a lifecycle, a current location, and a history of
state changes over time, is naturally a reference type: two consignments
with identical weight and destination are still two different shipments,
and code that holds a reference to one should see updates to its tracked
state. `Money`, `Weight`, and `SlotId` have no such lifecycle. A `Money`
instance holding 450.75 USD is not shipment-specific state that changes
over time and needs tracking; it is a number with a unit, fully described
by its two fields, and two instances with the same two fields are simply
the same amount. Microsoft's own [guidance on choosing between class and
struct](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct)
states the rule this directly: "consider defining a struct instead of a
class if instances of the type are small and commonly short-lived or are
commonly embedded in other objects," and a value type is the right choice
"if it logically represents a single value, similar to primitive types like
int, double, and so on." `Money`, `Weight`, and `SlotId` all pass that test:
each one logically *is* a single value, not a bundle of state with a
lifecycle of its own.

## readonly record struct: equality without boilerplate

A hand-rolled value type the old way means writing `Equals(object?)`,
`Equals(Money other)` through `IEquatable<Money>`, `GetHashCode`, `==`, and
`!=` by hand, and getting every one of them consistent with every other one.
Miss a field in `GetHashCode` that `Equals` does check and a `Money` can
compare equal to another `Money` while hashing to a different bucket, an
invariant every dictionary and hash set depends on silently and that no
compiler enforces for a hand-written override. The [structure types
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/struct)
flags exactly this cost in its design guidelines, recommending a struct
override `Equals`, `GetHashCode`, and the equality operators for value-based
comparison, which is also the fix for [analyzer rule
CA1815](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1815),
"override equals and operator equals on value types": without those
overrides a struct falls back to `ValueType.Equals`, which compares fields
through reflection, both slower and easy to get subtly wrong for a type
with a `string` field like `Money.Currency`, since the default comparison
has to special-case reference fields correctly to behave as expected.

`record struct` closes that gap by generating the overrides the analyzer
asks for, from the declaration alone. `Money`, `Weight`, and `SlotId` are
each declared as a `readonly record struct`:

```csharp
public readonly record struct Weight(decimal Kilograms)
{
    public decimal Kilograms { get; init; } = Kilograms >= 0m
        ? Kilograms
        : throw new ArgumentOutOfRangeException(nameof(Kilograms), Kilograms, "Weight cannot be negative.");

    public override string ToString() => $"{Kilograms:0.00} kg";
}
```

The compiler synthesizes `Equals(Weight other)`, the `IEquatable<Weight>`
implementation it backs, `GetHashCode`, `operator ==`, `operator !=`, and a
default `ToString` (overridden here for a friendlier format), all compared
member-by-member over `Kilograms`. `ValueEqualityTests` proves the generated
members behave correctly rather than merely compiling: two independently
constructed `Weight` values holding the same kilograms are `Equal`,
compare `true` through `==`, and share a hash code; two differing in
kilograms compare unequal through both `Equals` and `!=`. The same pattern
repeats for `Money` over both of its fields and for `SlotId` over `Bay` and
`Tier`, and a fourth type, `ConsignmentRecord`, composes all three as a
`readonly record struct` of its own,
`ConsignmentRecord(int ConsignmentId, SlotId Slot, Weight Weight, Money Charge)`,
getting a correct recursive equality over every nested value type with the
same zero lines of hand-written comparison code. `record struct`, not a
hand-written `readonly struct` with a manual `IEquatable<T>`
implementation, is the idiomatic choice precisely because the generated
members are mechanically derived from the member list and can never drift
out of sync with each other the way independently hand-written overrides
can.

The `readonly` modifier on top of `record struct` is a second, separate
guarantee: every instance member not explicitly mutating is treated as
non-mutating, and the compiler rejects an attempt to add one that would be.
`record struct` alone, without `readonly`, still allows mutable
`{ get; set; }` properties; adding `readonly` on `Money`, `Weight`,
`SlotId`, and `ConsignmentRecord` closes that door explicitly, so a reader
of the type declaration does not have to check every property individually
to know the type is immutable. The [`readonly` struct
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct)
notes this also lets the compiler skip defensive copies in some calling
contexts, since it can trust that no member call will mutate the instance
it's invoked on.

## The default-value pitfall and the type's own sentinel

Every struct in .NET, record or not, carries an implicit parameterless
state: `default(Money)`, `new Money[3]`, and any `Money` field on a class
that nobody explicitly initializes all produce a `Money` with every field
set to its own bit-pattern zero, and none of that path ever runs through
the validating constructor above. That is not a bug in this chapter's
example; it is a feature of structs that the CLR guarantees for every value
type, documented plainly: a struct always has an accessible parameterless
constructor that zero-initializes every field, even when the declared type
defines its own constructors that look like they should be the only way in.
`Money`'s own doc comment names the consequence directly: `default(Money)`
produces `Amount == 0m` and `Currency == null`, and `Currency` being `null`
is not a value `Money.ToString()` or any other member can safely assume,
because the type's own contract promises a three-letter code, validated at
construction, and a bare default never passed through that validation.

The honest fix is not to pretend the default state doesn't exist or to
sprinkle null checks through every member that touches `Currency`. It is to
decide, type by type, whether the zero-initialized default is itself a
meaningful value, and supply a type-owned alternative where it isn't.
`Money.Zero` is that alternative:

```csharp
public static Money Zero { get; } = new(0m, "USD");
```

Code that needs a starting value for an accumulator, or a safe value to
return before a real amount is known, reaches for `Money.Zero`, which runs
through the validating constructor exactly like any other `Money` and comes
out the other side holding a real, formattable currency code.
`ValidationTests.Money_zero_is_a_valid_benign_default` checks exactly that:
`Money.Zero.Amount` is `0m` and `Money.Zero.Currency` is `"USD"`, a value
indistinguishable from one a caller constructed by hand. `Weight` and
`SlotId` need no equivalent sentinel, and their doc comments say so
explicitly rather than leaving a reader to wonder why only one of the three
types has a `Zero` property: `default(Weight)` is `Kilograms == 0m`, a real,
legitimate weight of nothing, and `default(SlotId)` is `Bay == 0, Tier ==
0`, a real, addressable slot at the first bay and first tier. Nothing about
either default is unsafe to read or format, so adding a sentinel for either
one would be ceremony without a corresponding bug to prevent. The lesson
generalizes past this one example: before adding a `Zero`, `Empty`, or
`None` static property to a value type, check whether the zero-initialized
default is actually dangerous for that specific type's invariants, the way
a `null` string field is, rather than adding the sentinel reflexively to
every value type regardless of whether its default needs one.

## ref struct: a type that cannot leave the stack

`EdiRecordParser` carries a `ReadOnlySpan<char>` as a field, and that single
fact forces it into a different category of struct than `Money`, `Weight`,
or `SlotId`. A span is itself a stack-only type: internally it holds a
reference to the start of a block of memory together with a length, and
that reference may point partway into an array, into a string's internal
buffer, or onto the stack itself via `stackalloc`. If a span field could be
copied onto the heap, boxed into an `object`, or kept alive past the
lifetime of whatever memory it refers to, a later read through it would
read freed or relocated memory, the exact class of bug the span was
designed to prevent, not reintroduce. The CLR's rule for `ref struct`, the
category `EdiRecordParser` belongs to, is the structural way to make that
bug impossible to write rather than merely unlikely: a `ref struct` can
only ever live on the stack.

```csharp
public readonly ref struct EdiRecordParser
{
    private readonly ReadOnlySpan<char> _segment;

    private EdiRecordParser(ReadOnlySpan<char> segment) => _segment = segment;

    public static bool TryParse(ReadOnlySpan<char> segment, out ConsignmentRecord record)
    {
        var parser = new EdiRecordParser(segment);
        return parser.TryParseCore(out record);
    }
}
```

The [`ref struct` type documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct)
lists the restrictions this stack-only guarantee implies, and every one of
them has a reason tied back to the same aliasing concern. A `ref struct`
cannot be boxed, because boxing moves a value onto the heap and a heap
object can outlive the stack frame whose memory a span inside it might
reference. It cannot be a field of a class or a non-`ref` struct, for the
same reason: a class instance lives on the heap, and any field inside it
inherits the heap's lifetime. It cannot be used as a type argument, captured
by a lambda or local function, or stored on the heap in a collection,
because a captured variable is lifted into a compiler-generated class to
survive past the enclosing method call, and that lift is itself a heap
allocation the `ref struct` rules exist to forbid. It cannot cross an
`await` boundary or be used as a field inside an iterator's state machine,
because both an `async` method and an iterator compile down to a
heap-allocated state machine object that needs to carry its local variables
across suspension points, and a `ref struct` field would again end up
living on the heap inside that generated class. `EdiRecordParser`'s own
`TryParse` is fully synchronous, calls only itself, and never hands its
span to a lambda, which is exactly the shape the type requires. None of
these restrictions are arbitrary friction; each one closes a specific path
by which a stack-only reference could end up outliving the stack frame it
points into.

## Span<T> and ReadOnlySpan<T>: a view, not a copy

A `Span<T>` or `ReadOnlySpan<T>` is a window onto memory that already
exists somewhere else: an array, a string's backing buffer, or stack-
allocated memory, described by a starting reference and a length and
nothing more. The [`Span<T>`
documentation](https://learn.microsoft.com/en-us/dotnet/api/system.span-1)
calls it "an allocation-free representation of contiguous regions of
arbitrary memory," and that allocation-free property is the whole value
proposition against the shipping domain's EDI feed. `EdiRecordParser`
receives its input as a `ReadOnlySpan<char>` and never widens it into a
`string` on the happy path. `TryParseCore` slices the one incoming span
into six smaller spans, one per field, each a view into the same
underlying character buffer at a different offset and length:

```csharp
if (!int.TryParse(_segment.Slice(ConsignmentIdOffset, ConsignmentIdLength), NumberStyles.None, CultureInfo.InvariantCulture, out var consignmentId))
{
    return false;
}

if (!decimal.TryParse(_segment.Slice(WeightOffset, WeightLength), NumberStyles.Number, CultureInfo.InvariantCulture, out var kilograms))
{
    return false;
}
```

`Slice` does not copy a single character; it produces a new span value, six
bytes of reference-plus-length metadata, pointing at the same backing
storage the original segment pointed at, offset and shortened. Every
numeric `TryParse` overload used here accepts a `ReadOnlySpan<char>`
directly, so the digits never have to become their own `string` before
`int.TryParse` or `decimal.TryParse` can read them. Both calls pass
`CultureInfo.InvariantCulture` explicitly, because a fixed-width feed from
a carrier has one wire format regardless of which machine's regional
settings happen to be active when the parse runs, and both calls pass an
explicit `NumberStyles`, `NumberStyles.None` for the zero-padded integer
fields that should never accept a sign or a decimal point, and
`NumberStyles.Number` for the two fixed-two-decimal amount fields, rather
than trusting a default that might silently accept formats the fixed-width
layout never produces. The one field that ends up as a `string`, the
currency code, still allocates nothing: `TryParseCurrency` compares the
three-character slice against each supported code with
`ReadOnlySpan<char>.SequenceEqual`, and on a match returns the matching
string *literal*, already interned by the runtime rather than built fresh,
so even the single `string`-typed field in the result costs nothing beyond
a handful of character comparisons.

`int.TryParse` and `decimal.TryParse` over `ReadOnlySpan<char>` are not a
workaround bolted on for this one parser; they are the current idiomatic
entry point for culture-aware numeric parsing in .NET, available alongside
the long-standing `string`-based overloads precisely so that span-based
code never has to materialize a string just to call them. C# 14 goes a step
further by making the span and array worlds interchangeable at more call
sites: the language now allows an implicit conversion from an array or
certain other collection types to `Span<T>` or `ReadOnlySpan<T>` in more
positions than earlier versions permitted, described in the [C# 14
implicit span conversions
documentation](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions),
which removes some of the explicit `.AsSpan()` calls earlier C# versions
needed at a method boundary. `EdiRecordParserTests` leans on exactly that
ergonomics improvement: its test fixtures build segments as plain `string`
values and hand them straight to `EdiRecordParser.TryParse(ReadOnlySpan<char>
segment, ...)` with no explicit conversion written at the call site,
because a `string` converts to `ReadOnlySpan<char>` without ceremony.

The malformed-input tests are what make `TryParse`'s contract trustworthy
rather than merely documented. `TryParse_segment_too_short_returns_false_and_does_not_throw`,
its too-long counterpart, a non-numeric `ConsignmentId`, a non-numeric
weight field, and an unrecognized currency code each produce `false` and a
`default` `ConsignmentRecord` out parameter, never an exception, which
matters because a carrier feed with one malformed record among a hundred
thousand well-formed ones should not take down the whole batch; the caller
checks the boolean and decides what to do with the bad row. The measured
proof that all of this actually costs nothing on the heap lives in
`NoAllocationTests.TryParse_steady_state_allocates_nothing_on_the_current_thread`,
covered in the next section, where a hundred thousand parses in a row move
the needle on `GC.GetAllocatedBytesForCurrentThread()` by exactly zero
bytes.

## Build, run, observe

Every command below runs with the local .NET 10 SDK; there is no container
anywhere in this loop. The example is standalone-testable from its own
directory:

```sh
cd examples/08-value-types-struct-span
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 29
  failed: 0
  succeeded: 29
  skipped: 0
  duration: 1s 892ms
```

The 29 cases split across four files, each proving a different claim this
chapter made. `ValueEqualityTests` proves the generated equality members on
`Money`, `Weight`, `SlotId`, and `ConsignmentRecord` are correct, not merely
present: independently constructed values with identical fields are equal
and share a hash code, values differing in any one field compare unequal,
and a `with`-derived copy leaves the original untouched. `ValidationTests`
proves the throwing constructors reject the values that would make the
shipping domain nonsensical, negative money, a malformed currency code,
negative weight, negative bay or tier, and separately proves `Money.Zero`
is itself a valid, non-throwing value rather than another trap. `EdiRecordParserTests`
proves `EdiRecordParser.TryParse` lands every field of a known-good segment
in its correct typed slot, recognizes every supported currency code, and
returns `false` without throwing for a too-short segment, a too-long
segment, a non-numeric field, an unrecognized currency code, and an empty
span. `NoAllocationTests` is the one test in the suite that measures rather
than asserts on a return value: it warms up the JIT and any one-time static
initialization with ten thousand throwaway parses, forces a full garbage
collection, records `GC.GetAllocatedBytesForCurrentThread()`, runs a
hundred thousand more parses, records the counter again, and asserts the
two readings are identical. That assertion is the chapter's central claim
turned into a number a test either confirms or fails on: the steady-state
parse genuinely allocates zero bytes, not "close to zero" or "fewer than
before."

## When not to use it

Value types earn their keep when a type is small, logically immutable, and
has no identity beyond its own data; none of those properties survives
automatically once a struct grows. A struct with a dozen fields copied by
value on every assignment, every method parameter, and every array element
access pays a real copying cost that a reference type, copied as a single
pointer-sized reference, does not. `Money`, `Weight`, and `SlotId` stay
small enough, one or two scalar fields each, that the copy is cheap enough
not to matter; a hypothetical shipment-manifest struct carrying twenty
fields would be the wrong shape for a value type regardless of whether its
data happens to have no identity, because the copying cost would start
competing with the allocation cost a struct is supposed to avoid.
Reference identity is the other disqualifier: a type whose instances need
to be told apart even when their data matches, a tracked `Consignment`
moving through a warehouse with a lifecycle and a mutable current state, is
a reference type by nature, and forcing it into a struct to chase the same
allocation savings would mean giving up the very identity semantics the
domain concept needs. Inheritance is a third: a struct cannot be a base
class or derive from another struct, only implement interfaces, so a
concept that genuinely needs a type hierarchy, several carrier
implementations sharing a base class's behavior rather than an interface's
contract, is not a candidate no matter how small its data looks.

`ref struct` carries its own sharper boundary: reach for one only when a
type's entire job is synchronous, non-capturing, and finished before the
method that created it returns. `EdiRecordParser` fits that shape exactly,
parse one segment, return a result, done, with no span ever asked to
survive past the call. A parser that needs to yield records lazily through
an iterator, or hand a partially parsed span off to an asynchronous
downstream step, cannot be a `ref struct` at all, because the compiler will
reject exactly the iterator state machine or `await` boundary that shape
requires; the fix there is not to fight the restriction but to materialize
the needed data, a `ConsignmentRecord` or a `string`, before crossing that
boundary, and keep the `ref struct` itself confined to the fully
synchronous slice. A span passed into a method and stored in a field that
outlives the call is a compile error for a `ref struct` field and a latent
bug waiting to happen for any other value a developer tries to smuggle a
span out through; the compiler catches the `ref struct` case directly, but
the discipline of not letting a span escape its stack frame has to hold
even where the compiler cannot enforce it, such as a span returned from a
method that then gets held past the lifetime of its backing memory.

> **AI sidebar.** Ask an AI pairing partner to "make this a struct for
> performance" on an existing class and a common result changes the type
> keyword without checking any of the three disqualifiers above: a model
> will readily convert a class with a dozen fields and a few methods into a
> struct, because the before-and-after diff looks like a clean, confident
> optimization, without weighing the copying cost against whatever
> allocation it saves. It will just as readily leave mutable `{ get; set; }`
> auto-properties on a converted struct rather than reaching for `readonly
> record struct`, missing both the immutability and the generated equality
> that would come from the idiomatic form with no extra code. And when
> asked to add a `Span<T>` field to speed up a parser, a generated answer
> will sometimes store that span on a class or return it from a method in a
> way that outlives the call, a mistake the compiler would reject outright
> on a genuine `ref struct` but that a hand-rolled struct hiding a span
> indirectly, boxed inside an object, say, might not catch until a reader
> traces where the backing memory actually lives. Check a generated
> struct conversion against the same three questions this chapter asks: is
> the type small, does it need no identity beyond its data, and does
> nothing about it require inheritance; check a generated span-bearing type
> against whether it is really confined to one synchronous, non-capturing
> call, the only shape a `ref struct` is built for.

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
