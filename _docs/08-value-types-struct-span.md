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

- Tell copy semantics from reference semantics, and decide when a money amount, a weight or a slot address is a value rather than an object with identity.
- Declare a `readonly record struct` that gets value equality, a consistent hash code and immutability with no hand-written `Equals`, `GetHashCode` or `==`/`!=`.
- Spot a struct whose `default` breaks its invariants, and give that type a sentinel of its own.
- State the constraints a `ref struct` carries and the reason for each.
- Parse a fixed-width record out of a `ReadOnlySpan<char>` with slicing and culture-invariant `TryParse`, allocating zero bytes.

## Why this matters

A rating engine quoting thousands of consignments a minute spends most of its working set on values with no identity: an amount of money, a weight in kilograms, a warehouse slot address. Two `Money` values holding 450.75 USD are the same amount. As reference types, each one is a heap allocation for the garbage collector to track, and two of them are equal only if someone overrode `Equals`. As value types, equality comes from the values, a copy is the bits copied, and the collector never sees them.

The same cost appears where data enters. A carrier sends consignment manifests as fixed-width EDI-style text, 28 characters a record. A parser that calls `string.Substring` per field allocates a string for every field and discards it as soon as it becomes a number. `Span<T>` and `ReadOnlySpan<T>` are views over memory that already exists: they slice without copying and parse directly, so no field becomes a heap object just to be read.

{% include excalidraw.html name="08-value-types-struct-span" caption="Figure 8.1: Value types compare by content instead of reference, and a fixed-width EDI segment is sliced in place into typed fields with no intermediate string allocation." %}

## Copy semantics versus reference semantics

Assigning a reference-type variable copies a reference: both variables point at one object, and a mutation through one shows through the other. Assigning a value-type variable copies the value: the two variables are independent from then on.

```csharp
var original = new Weight(10m);
var copy = original;
copy = copy with { Kilograms = 999m };

Assert.Equal(10m, original.Kilograms);
Assert.Equal(999m, copy.Kilograms);
Assert.NotEqual(original, copy);
```

`with` produces a third value and `original` never changes. That is what makes a value type safe to pass to an unrelated method, use as a dictionary key or hand across a thread: no caller can mutate a `Weight` out from under another.

The deciding question is whether the concept has an identity separate from its data. A `Consignment` tracked through a warehouse has a lifecycle, a location and a history; two consignments with the same weight and destination are still two shipments, so it's a reference type. `Money`, `Weight` and `SlotId` are fully described by their fields. Microsoft's [guidance on choosing between class and struct](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct) says to "consider defining a struct instead of a class if instances of the type are small and commonly short-lived or are commonly embedded in other objects," and to choose one "if it logically represents a single value, similar to primitive types like int, double, and so on." All three pass.

## readonly record struct: equality without boilerplate

A hand-rolled value type needs `Equals(object?)`, `IEquatable<T>.Equals`, `GetHashCode`, `==` and `!=`, all consistent. Miss a field in `GetHashCode` that `Equals` checks, and two equal values hash to different buckets, which breaks every dictionary and hash set and which no compiler catches. Without the overrides, a struct falls back to `ValueType.Equals`, which compares fields through reflection; [CA1815](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1815) flags that, and the [structure types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/struct) recommends the overrides.

A `record struct` generates them from the declaration:

```csharp
public readonly record struct Weight(decimal Kilograms)
{
    public decimal Kilograms { get; init; } = Kilograms >= 0m
        ? Kilograms
        : throw new ArgumentOutOfRangeException(nameof(Kilograms), Kilograms, "Weight cannot be negative.");

    public override string ToString() => $"{Kilograms:0.00} kg";
}
```

The compiler synthesizes `Equals(Weight)`, `IEquatable<Weight>`, `GetHashCode`, `==`, `!=` and a default `ToString` (overridden here), all member by member. Equal kilograms give equal values and equal hash codes; different kilograms compare unequal through both `Equals` and `!=`. `Money` compares over both of its fields and `SlotId` over `Bay` and `Tier`. `ConsignmentRecord(int ConsignmentId, SlotId Slot, Weight Weight, Money Charge)` composes all three and gets recursive equality with no comparison code. The generated members derive from one member list, so they can't drift apart the way hand-written overrides can.

`readonly` is a separate guarantee. A plain `record struct` still allows `{ get; set; }` properties; `readonly` makes the compiler reject any mutating member, so the declaration alone tells a reader the type is immutable. The [`readonly` struct documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct) notes it also lets the compiler skip defensive copies.

## The default-value pitfall and the type's own sentinel

Every struct has a zero-initialized state that bypasses its constructor: `default(Money)`, `new Money[3]` and an uninitialized `Money` field all produce one. For `Money` that means `Amount == 0m` and `Currency == null`, which breaks the type's promise of a validated three-letter code.

Decide per type whether the default is a meaningful value, and supply a type-owned alternative where it isn't:

```csharp
public static Money Zero { get; } = new(0m, "USD");
```

`Money.Zero` runs through the validating constructor and holds a real currency code, so it's safe as an accumulator seed or a placeholder.

| Type | `default` | Sentinel needed |
| --- | --- | --- |
| `Money` | `Amount == 0m`, `Currency == null` | Yes: `Money.Zero` |
| `Weight` | `Kilograms == 0m`, a real weight of nothing | No |
| `SlotId` | `Bay == 0, Tier == 0`, a real first slot | No |

Add a `Zero`, `Empty` or `None` only when the zero state breaks an invariant, as a `null` string field does.

## ref struct: a type that cannot leave the stack

`EdiRecordParser` holds a `ReadOnlySpan<char>` field, so it must be a `ref struct`. A span is a reference plus a length, and the reference can point into an array, into a string's buffer or at `stackalloc` memory on the stack. If a span could reach the heap, it could outlive that memory and read freed or relocated data. A `ref struct` can only live on the stack, which makes that bug impossible to write.

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

The [`ref struct` documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct) lists the restrictions. Each one closes a path to the heap:

| A `ref struct` can't | Because |
| --- | --- |
| Be boxed | Boxing moves the value to the heap. |
| Be a field of a class or a non-`ref` struct | The containing object may live on the heap. |
| Be captured by a lambda or local function | Captured variables are lifted into a heap-allocated closure class. |
| Cross an `await` or live in an iterator | Async methods and iterators compile to heap-allocated state machines that carry locals across suspension. |

`EdiRecordParser.TryParse` is synchronous, calls only itself and never hands its span to a lambda, which is the shape the type requires.

## Span<T> and ReadOnlySpan<T>: a view, not a copy

The [`Span<T>` documentation](https://learn.microsoft.com/en-us/dotnet/api/system.span-1) calls a span "an allocation-free representation of contiguous regions of arbitrary memory." `EdiRecordParser` takes a `ReadOnlySpan<char>` and slices it into six fields:

| Field | Offset | Length |
| --- | --- | --- |
| Consignment ID | 0 | 6 |
| Bay | 6 | 2 |
| Tier | 8 | 2 |
| Weight | 10 | 7 |
| Amount | 17 | 8 |
| Currency | 25 | 3 |

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

`Slice` copies no characters; it returns a new span over the same storage, offset and shortened. The numeric `TryParse` overloads take `ReadOnlySpan<char>` directly, so the digits never become a `string`.

- **`CultureInfo.InvariantCulture`**, because a carrier feed has one wire format whatever the machine's regional settings.
- **Explicit `NumberStyles`**: `None` for zero-padded integers that must not accept a sign or a decimal point, `Number` for the two-decimal amounts, instead of a default that might accept formats the layout never produces.
- **The currency code** is compared with `ReadOnlySpan<char>.SequenceEqual` against each supported code, and a match returns the string literal, which the runtime has already interned. The one `string` field in the result allocates nothing.

{% include excalidraw.html name="08-fixed-width-edi-layout" caption="Figure 8.2: A 28-character EDI segment is sliced at fixed offsets directly into typed fields, assembling one consignment record without allocating an intermediate string." %}

Implicit span conversions, new in C# 14, let arrays and some other collection types convert to `Span<T>` or `ReadOnlySpan<T>` in more positions ([what's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions)), which removes some explicit `.AsSpan()` calls at method boundaries. A `string` passes straight to `TryParse(ReadOnlySpan<char> segment, ...)` with no conversion at the call site.

Malformed input never throws. A segment that is too short or too long, a non-numeric ID or weight, an unknown currency code or an empty span each return `false` with a `default` record. One bad row among a hundred thousand shouldn't stop the batch; the caller checks the boolean and decides.

## Build, run, observe

```sh
cd examples/08-value-types-struct-span
dotnet test
```

All 29 tests pass, and `scripts/verify.sh` runs them with every other example. The allocation check warms up with ten thousand parses, forces a full collection, then runs a hundred thousand more: `GC.GetAllocatedBytesForCurrentThread()` reads the same before and after, so the steady-state parse allocates zero bytes.

## When not to use it

A value type fits a type that is small, immutable and has no identity beyond its data. Each of these rules one out:

- **Many fields.** A struct is copied on every assignment, parameter and array access. `Money`, `Weight` and `SlotId` hold one or two scalars; a twenty-field manifest struct would pay more in copying than it saves in allocation.
- **Identity.** A tracked `Consignment` with a mutable current state must be told apart from another with the same data. Making it a struct gives up the semantics the domain needs.
- **Inheritance.** A struct can implement interfaces but can't derive or be derived from. A concept that needs a base class with shared behavior isn't a candidate.
- **`ref struct` beyond one synchronous call.** A parser that yields records through an iterator or hands a span to an `await`ed step can't be a `ref struct`; the compiler rejects it. Materialize a `ConsignmentRecord` or a `string` before the boundary and keep the `ref struct` in the synchronous part. A span returned from a method and held past its backing memory is a bug the compiler can't always catch, so the discipline applies outside `ref struct` too.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a value type.** "Make this a struct for performance" leaves open the size of the type, whether it needs identity, whether it's immutable and where a span may live. State them:

> Convert `Weight` to a `readonly record struct` with one `decimal Kilograms` property validated in its initializer; don't hand-write `Equals`, `GetHashCode` or the operators. Keep `Consignment` a class. Write `EdiRecordParser` as a `readonly ref struct` used inside one synchronous `TryParse` call, slicing a `ReadOnlySpan<char>` and parsing with `CultureInfo.InvariantCulture` and explicit `NumberStyles`, with no `Substring`.

Check a converted struct against three questions: is it small, does it need no identity, does it need no inheritance.
</blockquote>

## For deeper coverage

- The official Microsoft Learn documentation:
  [Structure types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/struct),
  [`readonly` struct](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#readonly-struct),
  [`ref struct` types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct),
  [`Span<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.span-1) and
  [`ReadOnlySpan<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1),
  [choosing between class and struct](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct),
  [CA1815: override equals and operator equals on value types](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1815),
  and the [C# 14 implicit span conversions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions).
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
