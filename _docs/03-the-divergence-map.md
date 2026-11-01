---
title: "The divergence map"
order: 3
layout: tutorial
sectionid: docs
part: "why-not-java"
description: "event, delegates, yield and LINQ, nullable reference types, extension members, expression trees, source generators, reified generics, static abstract members, and value types, each named with a forward reference to the chapter that develops it."
---

## Learning objectives

- Name the ten C# language and runtime features mapped in this chapter, and state which later chapter develops each one.
- Tell a compiler-enforced feature (a nullable reference type warning, a reified generic instantiation) apart from a library-level one built out of ordinary types (LINQ, expression trees).
- Explain how a C# delegate's multicast invocation list gives a callback problem different runtime machinery than a hand-written listener interface.
- Read an `event`-based subscription and name what the C# compiler generates on your behalf: the backing delegate field and the `add`/`remove` accessors.
- Predict, before reading them, which pattern chapters in this book will look least like their 1994 catalogue shape, and why.

## Why this matters

A C# 14 source file and a C# source file written in the 1994 catalogue's object-and-interface style read alike at a glance. Braces, semicolons, `interface`, generics in angle brackets, a `class` keyword doing the same job in both: the family resemblance is real, and it is also the trap. A developer trained on the catalogue's abstract-class-and-factory habits can compile working modern C# without ever reaching for a feature the catalogue's era had no way to assume, and come away convinced the language hasn't moved much past 1994. It has. C# has shipped more than a decade of language and runtime additions the catalogue's authors had no way to anticipate, and several of the pattern chapters in this book exist in their current shape because of them, not despite them.

This chapter is a map, not a tutorial. Each entry below names a feature, states what it changes, and points at the chapter that puts it to work. Read it once before the pattern chapters start, and the "why does this look nothing like the catalogue's own diagram" moments later stop being surprises. They become entries already filed under a name.

{% include excalidraw.html name="03-the-divergence-map" caption="Figure 3.1: Ten C# 14 / .NET 10 language and runtime features, each mapped to the chapter that puts it to work." %}

## Ten features, briefly

| Feature | What changes | Developed in |
|---|---|---|
| The `event` keyword | Compiler-generated `add`/`remove` accessors restrict subscription to `+=`/`-=` from outside the declaring type | "Observer: the event keyword" |
| Delegates | A method reference is a first-class runtime type with its own multicast invocation list, built into the CLR | "Strategy: delegates, interfaces, and keyed services"; "Template Method and the delegate alternative" |
| `yield` and LINQ | An iterator block compiles to a hidden state machine; LINQ turns `IEnumerable<T>` into a lazily evaluated query language | "Iterator: yield return, LINQ, and IAsyncEnumerable" |
| Nullable reference types | `string?` versus `string` is a compile-time annotation tracked by flow analysis, with no runtime wrapper | "NRTs, records, and init/required/with" |
| Extension members (C# 14) | An `extension` block can add properties and static members to a type it does not own, not only instance methods | "Extension methods and C# 14 extension members" |
| Expression trees | `Expression<Func<T, bool>>` captures code as an inspectable object graph instead of compiling it to IL | "Expression trees: code as data" |
| Source generators | A Roslyn component runs during compilation and injects visible `partial` classes, with no reflection cost at runtime | "Source generators: patterns at build time"; "Roslyn analyzers: patterns enforced at compile time" |
| Reified generics | `List<int>` and `List<string>` are distinct runtime types, each with code specialized to its own type argument | "static abstract members and generic math" |
| `static abstract` interface members | An interface can require a static operator or factory method, so generic code can call `T.Zero` or `a + b` for any `T` that implements it | "static abstract members and generic math" |
| Value types | `struct`, `readonly struct`, and `Span<T>` let a user-defined type opt into value semantics and live inline on the stack | "Value types: struct, readonly struct, and Span<T>" |

Three of these ten live entirely in library and runtime design, not new syntax: delegates are an ordinary type the CLR has had since .NET's first release, expression trees are an ordinary class graph (`System.Linq.Expressions`) the compiler happens to build instead of emitting IL, and reified generics are the same CLR design decision from that first release, not a recent addition. The other seven depend on dedicated compiler syntax added across later C# versions: `event`, `yield return`, nullable annotations, `extension`, source generators, `static abstract`, and `struct`/`readonly struct`/`Span<T>` each needed a keyword, attribute, or compiler pass that the language didn't have until it shipped one. The distinction matters for a reader deciding how deep a feature goes: a library-level capability can, in principle, be approximated out of ordinary types without the dedicated syntax, while a syntax-level one depends entirely on the compiler recognizing it, with no workaround available until it does.

### Delegates and the `event` keyword

The catalogue's way to broadcast a notification to unknown listeners is a hand-built subject: a list of listener objects, each implementing a single-method interface, notified by iterating the list and calling the same method on every entry. A `ConsignmentTracker` that notifies interested code as cargo moves and eventually clears customs needs none of that machinery in modern C#. A delegate type is a runtime type in its own right with a built-in invocation list; declaring a member `event Action<string, ConsignmentStatus>? StatusChanged` gives external code exactly two operations, `+=` and `-=`, and the compiler generates the `add`/`remove` accessors that enforce it. Code outside the declaring class cannot call `Invoke` directly or clear the list, a restriction the C# language specification's description of event accessors states as a compiler rule, not a convention a team has to remember to follow.

```csharp
public class ConsignmentTracker
{
    public event Action<string, ConsignmentStatus>? StatusChanged;

    public void MarkCleared(string trackingNumber) =>
        StatusChanged?.Invoke(trackingNumber, ConsignmentStatus.Cleared);
}

// A tracking dashboard subscribes without implementing a listener interface.
tracker.StatusChanged += (trackingNumber, status) =>
    Console.WriteLine($"{trackingNumber}: {status}");
```

One field declaration replaces a listener interface, a registration method, and a manual notification loop. Delegates also compose in a way a one-method-per-interface design doesn't: a `Func<Consignment, bool>` and a `Func<Consignment, decimal>` are two instantiations of the same generic delegate type, not two unrelated interfaces a reader has to look up separately. "Strategy: delegates, interfaces, and keyed services" builds a strategy entirely out of delegate fields; "Template Method and the delegate alternative" and "Command, undo, and Task-returning commands" each lean on the same mechanism for a different GoF shape.

### `yield` and LINQ

The catalogue's Iterator pattern is a hand-written cursor class: a concrete type that tracks position in a mutable field and exposes `First`, `Next`, `IsDone`, and `CurrentItem` operations a caller drives one step at a time. C#'s `yield return`, inside a method that returns `IEnumerable<T>`, turns ordinary control flow, loops, conditionals, early returns, into a compiler-generated state machine; a method that pages through a consignment's tracking history reads like a description of the sequence, not an implementation of a cursor class, a transformation the C# language reference's chapter on iterators documents in detail. LINQ builds on the same `IEnumerable<T>` to add a declarative query layer: `history.Where(e => e.RecordedAt >= since).OrderBy(e => e.RecordedAt)` evaluates lazily, one element at a time, as the caller enumerates it. That laziness lets a tracking feed compose several filters before any of them runs, instead of committing to a result up front. "Iterator: yield return, LINQ, and IAsyncEnumerable" covers all three together, including the async form that applies the same laziness to a live feed of handling events arriving over time.

### Nullable reference types

Neither C++ nor Smalltalk's type system, the languages the 1994 catalogue was written against, distinguished a reference that is always present from one that might be absent, the same gap that gave the Null Object pattern its reason to exist. C#'s nullable reference types, enabled project-wide in this book's `Directory.Build.props`, put that distinction directly in the type signature: `string` means not null, `string?` means it might be, and the compiler's flow analysis, described in Microsoft's [nullable reference types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references), tracks which is which through a method body, warning when a `string?` reaches a place expecting `string` without a preceding check. The guarantee lives entirely in the type checker and the warnings it raises; a `string?` and a `string` are the same reference at runtime, with no wrapper type a caller has to unwrap. "NRTs, records, and init/required/with" works through what this means for a `Consignment` record's constructor, and how a `with` expression produces an amended copy without ever reopening the question of whether a field might be null.

### Extension members

Adding a member to a type a codebase doesn't own and can't subclass is an old problem, traditionally solved with a static utility method called as a free function instead of through the instance, the kind of `CarrierStatusUtils.Format(status)` call that scatters formatting logic away from the type it describes. C# has had extension methods since C# 3, called with instance syntax; C# 14 widens the mechanism to extension members, letting an `extension` block add properties and static members to a type as well as instance methods. A status type defined inside a carrier's own SDK, one this book's code doesn't own and can't subclass, can still gain a read-only `FormattedEta` property from a separate file in this codebase. "Extension methods and C# 14 extension members" works through both forms against exactly that problem: extending a carrier SDK type the book never gets to edit.

### Expression trees

A C# lambda assigned to `Func<Consignment, bool>` compiles straight to a method, like any other lambda assignment. Assigned to `Expression<Func<Consignment, bool>>` instead, the compiler builds a data structure describing the lambda's logic, which comparison, which operands, rather than compiling it to IL at all, a distinction Microsoft's [expression trees documentation](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/) spells out as two different compiler targets for the same syntax. A tariff rule, weight over 500 kilograms and a destination in a bonded zone, represented this way can be inspected, logged, or partially evaluated by a rule engine before anything runs, instead of only ever being executed as opaque code, with no need to parse compiled bytecode or hand-assemble a query object to get there. "Expression trees: code as data" builds the tariff rule engine against this mechanism to make it concrete.

### Source generators

Generating code at build time instead of writing it by hand has a history of fragile tooling: rewritten bytecode, special annotation processors, or an IDE plugin that only works if every contributor's toolchain matches exactly. A C# source generator is an ordinary Roslyn component that runs during compilation, reads the syntax trees the compiler already produced, and emits additional source the same compilation includes, as visible, debuggable `partial` classes with zero reflection cost at runtime, a pipeline Microsoft's [source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) documents end to end. A generator that reads a declarative carrier manifest and emits one gateway adapter per carrier runs on every build and produces code a reader can open and step through, rather than a reflection-based factory resolved only at runtime. "Source generators: patterns at build time" and "Roslyn analyzers: patterns enforced at compile time" cover generation and enforcement as two sides of the same compiler API.

### Reified generics and `static abstract` members

C# generics are reified: `List<int>` and `List<string>` are distinct runtime types, each with code specialized for its own type argument, a guarantee the .NET generics documentation describes as part of the CLR's design from .NET's first release. An `int` stored in a `List<int>` never needs boxing to satisfy a shared generic implementation, because there is no shared implementation to satisfy; the JIT produces one specialized to `int` and a separate one specialized to `string`. That reification is what makes `static abstract` interface members possible: an interface can declare a static abstract operator or factory method, and a generic method constrained to that interface can call `T.Zero` or `a + b` for any `T` that implements it, resolved per instantiation at compile time, with no runtime token or boxed wrapper carried along to make the dispatch work. An `IRateable<TUnit>` written once against a generic-math interface can run specialized, unboxed code for a `Money` amount and a `Weight` value in the same compiled assembly, without the weight calculation paying for the money calculation's branches or vice versa. "static abstract members and generic math" covers both the reification that makes this possible and the generic math interfaces the base class library ships on top of it.

{% include excalidraw.html name="03-reification-pipeline" caption="Figure 3.2: Generic instantiation in C#: the compiler and CLR produce a distinct, specialized runtime type for each type argument, with no boxing of value-type elements." %}
<!-- Figure 3.2 spec: a single compile/runtime pipeline. Source declares List<int> and List<string>; the Roslyn compiler and CLR JIT produce two distinct specialized runtime types, each storing its element inline with no boxing. Label each pipeline stage (source, Roslyn compile, CLR JIT, specialized runtime types). No chapter numbers or internal codes in the drawing. -->

### Value types

C#'s `struct` lets any type opt into value semantics: `Money`, `Weight`, and `SlotId` can each be defined as a `readonly struct`, copied by value, compared by value by default, and allocated inline inside its containing object or on the stack instead of the heap. `Span<T>` goes further, giving a type that can reference a slice of an array, or of stack-allocated memory, such as one field inside an EDI segment, without copying it or allocating a new object. A pattern built around a small, frequently allocated value, a money amount, a weight, a slot identifier, gets to make that layout choice explicitly in this book's examples, rather than defaulting to heap allocation and reference comparison for every type. "Value types: struct, readonly struct, and Span<T>" works through the tradeoffs this book's shipping-domain types make with that choice available.

None of these ten items is exotic; each ships in the box with .NET 10, and each gets exercised somewhere in this book's examples. The map doesn't try to rank them by importance or teach them in depth; both jobs belong to the chapters it points to. What it buys a reader coming from the 1994 catalogue's habits is a vocabulary to recognize unfamiliar C# on sight, instead of mistaking it for an unfamiliar library convention. The next chapter puts a related piece of that vocabulary to work for a different purpose: naming the design itself, not just the language feature it is built from.

## For deeper coverage

- Each chapter named in the table above cites the specific Microsoft Learn page or C# language specification section for its feature as it goes deeper; this chapter only names the starting points.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
