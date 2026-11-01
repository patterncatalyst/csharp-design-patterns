---
title: "C# 15 / .NET 11 delta"
order: 53
layout: tutorial
sectionid: docs
part: appendices
appendix: "G"
description: "What the C# 15 and .NET 11 short-term-support release changes for each affected chapter, kept additive so the chapters stay on C# 14."
---

## Learning objectives

By the end of this appendix you can:

- State the release status of C# 15 and .NET 11 as of this writing, and name
  the one feature in the set that is still preview-only.
- Explain why Runtime Async in .NET 11 changes how an async method executes
  without changing the stackless, Task-returning model the book's
  concurrency chapters teach.
- Describe what a C# 15 union type and a `closed` hierarchy each add to the
  exhaustive-switch discussion in the Result, Tester-Doer, Visitor, and
  State chapters.
- Name the .NET 11 library additions, generic `Complex<T>`, the IEEE decimal
  types, `FullJoin`, and asynchronous Options validation, and the chapter
  each one extends.
- Locate the Microsoft Learn page behind every claim in this appendix, since
  every one of them describes software that had not reached general
  availability when this appendix was written.

## Why this matters

As of November 1, 2026, .NET 11 and C# 15 are in release candidate. The
[What's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
page on Microsoft Learn states the article "was last updated for
release candidate 2 (RC 2)," and general availability "is expected in
November 2026." Microsoft's own .NET Conf 2026 announcement ties that
release to the event's November 10 to 12 dates, and .NET 11 ships as a
Standard Term Support release with two years of support, the length
Microsoft set for STS releases starting with .NET 9. None of that is final
until it ships.

Every chapter in this book targets .NET 10 and C# 14, the current long-term-
support release, and stays there. This appendix is the one place that looks
forward, and it does so narrowly: each section below names one C# 15 or
.NET 11 change, says what it affects, and cites the Microsoft Learn page
that documents it. Nothing here asks a chapter's example to change. A
reader upgrading a project once .NET 11 ships can use this appendix as a
map of what moves and what does not.

## Runtime-native async: an implementation change, not a new model

.NET 11 ships Runtime Async, a change to how the runtime executes an async
method, not a change to what an async method is. The
[.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
page describes it as producing "cleaner stack traces and lower overhead,"
and states that Runtime Async "no longer requires
`<EnablePreviewFeatures>true</EnablePreviewFeatures>` for projects that
target `net11.0`," because the .NET 11 runtime libraries themselves are now
compiled with `runtime-async=on`. The page also lists JIT-level follow-on
work: a dedicated runtime-async code path for synchronous task-returning
methods, async continuations that skip capturing an `ExecutionContext` when
there is no ambient state to carry, and tail-merged suspension points that
shrink the generated code.

None of that touches the model the stackless-async chapter explains: C#
still compiles an `async` method into a state machine rather than switching
stacks the way a stackful coroutine would, and the reasons the .NET team
chose that shape, millions of concurrent operations on a small thread pool,
no per-operation stack to allocate, still hold exactly as stated. A `Task`-
returning method still returns a `Task`, cancellation still flows through a
`CancellationToken`, and `ConfigureAwait(false)` still means what the TAP
conventions chapter says it means. Runtime Async changes how the runtime
represents and schedules the state machine once the JIT has it, which is
why it ships as a runtime behavior with no required code change rather than
a new keyword or a new return type. A project moving to `net11.0` gets the
lower overhead automatically; the TAP conventions and cancellation,
progress, and `ValueTask` patterns the book teaches for async methods carry
over unchanged.

## Union types and closed hierarchies: Result, Tester-Doer, and Visitor

C# 15 adds two related exhaustiveness features, both described on the
[What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
page. A `union` declares a value that can be exactly one of several named
case types, with an implicit conversion from each case and a compiler-
checked guarantee that a `switch` over the union handles every case. A
`closed` class or record fixes its set of direct descendants to the
declaring assembly at compile time, so a `switch` over a closed hierarchy
needs no `default` arm; the compiler already knows every branch that
exists.

The Result debate chapter's third option, a `Result<T>` built as a
`readonly record struct` with an `IsSuccess` flag plus nullable `Value` and
`Error` fields, is exactly the shape a union exists to replace: instead of
a boolean discriminator a caller has to check before trusting which of two
fields is populated, a future `union Result<T>(T, Error)` would let the
compiler guarantee a `switch` on the result handles the success case and
the failure case and nothing else. The State chapter's finite-state-machine
example, today a `switch` expression over an enum, and the Visitor chapter's
grammar, today pattern-matched over a small set of node types, are the
other natural fit: a `closed` hierarchy of record types would carry the
same exhaustiveness guarantee the enum-based `switch` already leans on,
without a `default` arm standing in for "every other case," which is
useful precisely because a closed hierarchy can add a case later and have
the compiler point at every `switch` that now needs updating. None of the
book's examples use either feature; both are additive options once C# 15
ships.

## Extension indexers

The extension-members chapter builds instance extension properties, a
mutating-free instance extension method, and static extension members
inside an `extension(CarrierShipment)` block. C# 15 adds one more member
kind to that same block: an extension indexer, documented on the
[What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#extension-indexers)
page, lets an `extension` block declare a `this[...]` member that a caller
indexes into exactly as if the receiver type had declared it. The feature
needs a named receiver parameter, because an indexer, unlike a static
extension member, is always an instance member. It extends the chapter's
existing menu of extension-member kinds; nothing about the kinds the
chapter already covers changes.

## Static interface members and the numeric additions

C# 15 relaxes one constraint the static-abstract-members-and-generic-math
chapter runs into directly: historically, a runtime without default-
interface-implementation support could not run code that declared even a
*non-virtual* static member on an interface. The
[non-virtual static interface members](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#non-virtual-static-interface-members-on-older-runtimes)
section of the C# 15 page describes the fix: an interface can declare an
ordinary static helper method and call it through the interface name on a
runtime that does not support default implementations at all. The chapter's
own feature, `static abstract` members used through a self-referencing
generic constraint, still needs full runtime support for default interface
implementations; this delta only loosens the rule for a static member that
carries no per-type behavior to override.

The .NET 11 libraries extend the same chapter's generic-math story with new
types that implement the `System.Numerics` interfaces `Money` is built
against. The
[.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
page lists IEEE 754 decimal floating-point types, `Decimal32`, `Decimal64`,
and `Decimal128`, a generic `Complex<T>`, and generic overloads on
`Random`, `NextInteger<T>` and `NextBinaryFloat<T>`, that work with any
numeric generic type. A generic algorithm written once against
`IAdditionOperators<T, T, T>`, the way the chapter's `Totals.Sum<T>` is,
runs unchanged over any of these new types the same way it already runs
over `Money` and the built-in numeric types.

## Async options validation

The Options pattern chapter builds `CarrierGatewayOptionsValidator` as an
`IValidateOptions<CarrierGatewayOptions>` that `ValidateOnStart()` calls
synchronously before the host finishes starting. The
[.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
page adds an asynchronous counterpart: "asynchronous
`Microsoft.Extensions.Options` validation through `IAsyncStartupValidator`,"
alongside `AsyncValidationAttribute` and `IAsyncValidatableObject` in
`System.ComponentModel.DataAnnotations`. The gap this closes is a validator
that needs to check something over the network, confirming a carrier
gateway endpoint resolves and answers, say, rather than only
checking the shape of the bound configuration in memory. The chapter's
synchronous validator still covers every check that does not need an
awaited call; `IAsyncStartupValidator` is an additional option for the
checks that do.

## LINQ join improvements and collection expression arguments

The Iterator chapter's LINQ section composes operators like `Where` and
`Select` as iterators the runtime evaluates lazily. .NET 11 adds to the
join family specifically: the
[.NET 11 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
page lists "`FullJoin` and tuple-returning `Join` and `GroupJoin`
overloads, across `Enumerable`, `Queryable`, and `AsyncEnumerable`." A
`FullJoin` matches the book's existing `await foreach` style over
`IAsyncEnumerable<T>` without a new operator family to learn, and the
tuple-returning overloads remove the anonymous-type projection a `Join`
call needs today to carry both sides of a match forward.

C# 15's collection expression arguments touch the value-types chapter's
collection-literal examples the same way: a `with(...)` element as the
first entry in a collection expression, documented on the
[What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#collection-expression-arguments)
page, passes a constructor argument, a capacity or a comparer, straight
through the `[...]` syntax, for example `List<string> names =
[with(capacity: 8), .. values]`. It is additive syntax over the same
collection-expression feature the book already uses, not a new way to
build a collection.

## Labeled break and continue

C# 15 lets a `break` or `continue` name a label on an enclosing loop or
`switch`, described on the
[What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#labeled-break-and-continue)
page as a replacement for the boolean-flag or `goto` workarounds a nested
loop otherwise needs to exit from an inner level. It is a minor, purely
syntactic addition; none of the book's examples nest loops deeply enough
to need it, and an unlabeled `break` or `continue` keeps its current
meaning everywhere it already appears.

## Memory safety: a C# 15 preview, not yet settled

Memory safety is the one feature on this list that is not scheduled to ship
as a default C# 15 behavior. The
[What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15#memory-safety)
page is explicit that this is "a memory safety preview feature that begins
a multirelease effort," available only when a project sets
`<LangVersion>preview</LangVersion>` and, for the updated caller-obligation
rules specifically, enables the `updated-memory-safety-rules` compiler
feature. Under the preview, `unsafe` moves from marking a block or member
to marking the specific operations that touch unmanaged memory directly,
address-of, `fixed`, a `stackalloc`-to-pointer conversion, and `sizeof`, so
that dereferencing a pointer is the operation that still requires an
`unsafe` context, not merely declaring one. This book has no unsafe-code
chapter and makes no use of pointers, so the preview has no example to
touch; it is listed here because it is the one feature in the set still
unsettled enough that a reader should not expect it at C# 15's general
availability.

## For deeper coverage

- Microsoft Learn, [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15): the primary source for every C# 15 language feature in this appendix, including the explicit preview status of memory safety.
- Microsoft Learn, [What's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview): the primary source for Runtime Async, the LINQ join and numeric library additions, asynchronous Options validation, and the release-candidate status this appendix dates itself against.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
