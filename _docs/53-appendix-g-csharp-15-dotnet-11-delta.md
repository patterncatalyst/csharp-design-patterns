---
title: "C# 15 / .NET 11 delta"
order: 53
layout: tutorial
sectionid: docs
part: appendices
appendix: "G"
description: "What the C# 15 and .NET 11 short-term-support release changes for each affected pattern and idiom, kept additive so the examples stay on C# 14."
---

## Learning objectives

By the end of this appendix you can:

- State the release status of C# 15 and .NET 11 as of November 1, 2026, and
  name the one feature in the set that's still preview-only.
- Explain why Runtime Async changes how an async method executes without
  changing the stackless, `Task`-returning model.
- Say what a C# 15 union type and a `closed` hierarchy add to exhaustive
  `switch` over Result, State and Visitor shapes.
- Name the .NET 11 library additions (generic `Complex<T>`, the IEEE decimal
  types, `FullJoin`, asynchronous Options validation) and the idiom each
  extends.
- Find the Microsoft Learn page behind each claim.

## Why this matters

As of November 1, 2026, .NET 11 and C# 15 are in release candidate. The
[What's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
page states the article "was last updated for release candidate 2 (RC 2),"
and that general availability "is expected in November 2026." Microsoft's
.NET Conf 2026 announcement ties the release to the event's November 10 to
12 dates. .NET 11 is a Standard Term Support release with two years of
support, the length Microsoft's policy sets for every STS release. None of
this is final until it ships.

The examples target .NET 10 and C# 14, the current long-term-support
release, and stay there. Each entry below names one C# 15 or .NET 11 change,
what it affects and its source. Nothing here requires an example to change;
use it as a map when you upgrade a project after .NET 11 ships.

## Summary

| Change | Kind | Affects | Status |
| --- | --- | --- | --- |
| Runtime Async | .NET 11 runtime | Stackless async, TAP conventions | No code change; on for `net11.0` |
| Union types | C# 15 | Result, Tester-Doer | Additive |
| `closed` classes and records | C# 15 | State, Visitor | Additive |
| Extension indexers | C# 15 | Extension members | Additive |
| Non-virtual static interface members on older runtimes | C# 15 | Static abstract members | Additive |
| `Decimal32`/`Decimal64`/`Decimal128`, `Complex<T>`, generic `Random` overloads | .NET 11 libraries | Generic math | Additive |
| `IAsyncStartupValidator` | .NET 11 libraries | Options pattern | Additive |
| `FullJoin`, tuple-returning `Join`/`GroupJoin` | .NET 11 libraries | LINQ, `IAsyncEnumerable<T>` | Additive |
| Collection expression arguments | C# 15 | Collection expressions | Additive |
| Labeled `break` and `continue` | C# 15 | Nested loops | Additive |
| Memory safety rules | C# 15 preview | Unsafe code | Preview only |

## Runtime Async: an implementation change, not a new model

Runtime Async changes how the runtime executes an async method, not what an
async method is. The [.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview) describes "cleaner stack
traces and lower overhead," and says Runtime Async "no longer requires
`<EnablePreviewFeatures>true</EnablePreviewFeatures>` for projects that
target `net11.0`," because the runtime libraries are now compiled with
`runtime-async=on`. JIT follow-on work includes:

- a dedicated runtime-async path for synchronous `Task`-returning methods;
- continuations that skip capturing an `ExecutionContext` when there's no
  ambient state to carry;
- tail-merged suspension points that shrink the generated code.

The model stays the same. C# still compiles an `async` method into a state
machine instead of switching stacks like a stackful coroutine, and the
reasons hold: millions of concurrent operations on a small thread pool, no
per-operation stack to allocate. A `Task`-returning method returns a `Task`,
cancellation flows through a `CancellationToken`, and
`ConfigureAwait(false)` means what it did. Runtime Async changes how the
runtime represents and schedules the state machine after the JIT has it, so
it ships with no keyword or return-type change. A project moving to
`net11.0` gets the lower overhead automatically; TAP conventions,
cancellation, progress and `ValueTask` carry over unchanged.

## Union types and closed hierarchies

Both features are on the [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15) page.

| Feature | What it declares | What the compiler guarantees |
| --- | --- | --- |
| `union` | A value that is exactly one of several named case types, with an implicit conversion from each case | A `switch` over the union handles every case |
| `closed` class or record | A hierarchy whose direct descendants are fixed to the declaring assembly | A `switch` over it needs no `default` arm |

A `Result<T>` built as a `readonly record struct` with an `IsSuccess` flag
and nullable `Value` and `Error` fields is the shape a union replaces. With
`union Result<T>(T, Error)` the caller no longer checks a boolean to learn
which field is populated: the compiler requires the `switch` to handle
success and failure and nothing else.

A State machine written as a `switch` expression over an enum, and a Visitor
grammar pattern-matched over a few node types, fit a `closed` hierarchy of
records. It gives the same exhaustiveness without a `default` arm standing
in for "every other case", so adding a case later makes the compiler flag
every `switch` that needs updating. The examples use neither feature.

## Extension indexers

C# 15 adds a member kind to an `extension(CarrierShipment)` block, beside
instance extension properties, instance extension methods and static
extension members. An
[extension indexer](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#extension-indexers)
declares a `this[...]` member that callers index into as if the receiver type
had declared it. It needs a named receiver parameter, because an indexer is
always an instance member. The existing extension member kinds don't change.

## Static interface members and the numeric additions

A runtime without default interface implementation support used to reject
code that declared even a *non-virtual* static member on an interface. With
[non-virtual static interface members](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#non-virtual-static-interface-members-on-older-runtimes),
an interface can declare an ordinary static helper and call it through the
interface name on such a runtime. `static abstract` members used through a
self-referencing generic constraint still need full runtime support; the
change only loosens the rule for a static member with no per-type behavior.

The [.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview) lists new types that implement the
`System.Numerics` interfaces `Money` is built against:

- IEEE 754 decimal floating-point types `Decimal32`, `Decimal64` and
  `Decimal128`;
- a generic `Complex<T>`;
- generic `Random` overloads `NextInteger<T>` and `NextBinaryFloat<T>`.

A generic algorithm written once against `IAdditionOperators<T, T, T>`, such
as `Totals.Sum<T>`, runs unchanged over these types, as it already does over
`Money` and the built-in numeric types.

## Async options validation

`CarrierGatewayOptionsValidator` is an
`IValidateOptions<CarrierGatewayOptions>` that `ValidateOnStart()` calls
synchronously before the host finishes starting. The
[.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview) adds "asynchronous `Microsoft.Extensions.Options`
validation through `IAsyncStartupValidator`," alongside
`AsyncValidationAttribute` and `IAsyncValidatableObject` in
`System.ComponentModel.DataAnnotations`. It covers a check that needs the
network, such as confirming a carrier gateway endpoint resolves and answers,
not only the shape of the bound configuration. Keep the synchronous
validator for checks that need no awaited call.

## LINQ joins and collection expression arguments

The [.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview) lists "`FullJoin` and tuple-returning `Join`
and `GroupJoin` overloads, across `Enumerable`, `Queryable`, and
`AsyncEnumerable`." `FullJoin` works in the same `await foreach` style over
`IAsyncEnumerable<T>`, and the tuple-returning overloads remove the
anonymous-type projection a `Join` needs today to carry both sides of a match
forward.

[Collection expression arguments](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#collection-expression-arguments)
let a `with(...)` element, as the first entry of a collection expression,
pass a constructor argument such as a capacity or a comparer:

```csharp
List<string> names = [with(capacity: 8), .. values];
```

It's additional syntax over the existing collection expression feature.

## Labeled break and continue

C# 15 lets `break` or `continue` name a label on an enclosing loop or
`switch`. The [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#labeled-break-and-continue) page
presents it as the replacement for the boolean-flag or `goto` workarounds
needed to exit a nested loop from an inner level. An unlabeled `break` or
`continue` keeps its meaning, and no example nests loops deeply enough to
need it.

## Memory safety: a C# 15 preview

Memory safety is the one feature here that isn't scheduled to ship as a
default C# 15 behavior. The
[What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#memory-safety)
page calls it "a memory safety preview feature that begins a multirelease
effort." It needs `<LangVersion>preview</LangVersion>` and, for the updated
caller-obligation rules, the `updated-memory-safety-rules` compiler feature.
Under the preview, `unsafe` marks only the operations that read or write
through a pointer, not a whole block or member:

| No longer needs `unsafe` | Still needs `unsafe` |
| --- | --- |
| Taking an address | Dereferencing a pointer |
| `fixed` | Pointer member and element access |
| A `stackalloc`-to-pointer conversion | Invoking a function pointer |
| `sizeof` | |

No example uses pointers, so the preview touches nothing here. Don't expect
it at C# 15's general availability.

## For deeper coverage

- Microsoft Learn, [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15): the primary source for every C# 15 language feature in this appendix, including the explicit preview status of memory safety.
- Microsoft Learn, [What's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview): the primary source for Runtime Async, the LINQ join and numeric library additions, asynchronous Options validation, and the release-candidate status as of November 1, 2026.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
