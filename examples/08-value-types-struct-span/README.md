# 08-value-types-struct-span

Three shipping-domain value types, `Money`, `Weight`, and `SlotId`, as
`readonly record struct` types, plus `EdiRecordParser`, a `readonly ref struct`
that parses a fixed-width, EDI-style consignment segment straight out of a
`ReadOnlySpan<char>` with no intermediate string allocation.

**The value types.** `Money(decimal Amount, string Currency)`,
`Weight(decimal Kilograms)`, and `SlotId(int Bay, int Tier)` are all
`readonly record struct`s: value equality, a consistent hash code, and a
no-allocation copy on every assignment come from the declaration alone, with
no hand-written `Equals`, `GetHashCode`, or `==`/`!=`. Each validates through
an explicit property that shadows its primary-constructor parameter and
throws on a bad value (`new Money(-1m, "USD")`, `new Weight(-1m)`,
`new SlotId(-1, 2)` all throw). That guard never reaches `default(Money)` or
`new Money[3]`, since every struct carries an implicit parameterless state
that bypasses every constructor. For `Weight` and `SlotId` that default is
itself benign (a real zero weight, a real Bay‑0/Tier‑0 slot). For `Money` it
is not (`Currency` comes back `null`), so `Money.Zero` is the type's own
stand-in for a safe starting value.

**The parser.** `EdiRecordParser.TryParse(ReadOnlySpan<char> segment, out
ConsignmentRecord record)` reads a 28-character fixed-width segment:

| Field             | Offset | Length | Example    |
|-------------------|-------:|-------:|------------|
| ConsignmentId     |      0 |      6 | `004217`   |
| Bay               |      6 |      2 | `03`       |
| Tier              |      8 |      2 | `02`       |
| WeightKilograms   |     10 |      7 | `0125.50`  |
| Amount            |     17 |      8 | `00450.75` |
| Currency          |     25 |      3 | `USD`      |

`EdiRecordParser` is a `readonly ref struct` because it stores a
`ReadOnlySpan<char>` field, and only a `ref struct` may hold one. That same
restriction keeps every instance stack-only (no boxing, no capture in a
lambda, no crossing an `await`, no use inside an iterator), which is exactly
the synchronous, non-capturing shape this parser has. Numeric fields parse
straight out of sliced spans with `int.TryParse`/`decimal.TryParse` against
`CultureInfo.InvariantCulture`; the currency field is matched against a fixed
set of known codes and resolved to an already-interned literal, so even that
one string-typed field allocates nothing. A malformed segment (wrong length,
non-numeric field, unrecognized currency) returns `false` and never throws.

## What the tests prove

`ValueEqualityTests` shows two independently constructed `Money`, `Weight`,
and `SlotId` values with the same fields are `Equals`, `==`, and share a hash
code, and stop being equal the moment any field differs; a separate test
assigns a `Weight` to a second variable, derives a modified copy with `with`,
and shows the original is untouched, proving assignment copies rather than
aliases. `ValidationTests` proves the throwing constructors reject negative
money, negative weight, negative bay, and negative tier, and that `Money.Zero`
is a valid, non-throwing value. `EdiRecordParserTests` parses a known-good
segment and asserts each typed field lands in the right slot, parses one
segment per supported currency code, and proves a too-short segment, a
too-long segment, a non-numeric field, and an unrecognized currency all
return `false` without throwing. `NoAllocationTests` warms up the JIT and any
one-time static initialization, then measures
`GC.GetAllocatedBytesForCurrentThread()` across a 100,000-iteration parse
loop and asserts the delta is zero.

## Build and test

```sh
cd examples/08-value-types-struct-span
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
