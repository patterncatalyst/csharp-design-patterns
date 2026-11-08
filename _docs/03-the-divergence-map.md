---
title: "The divergence map"
order: 3
layout: tutorial
sectionid: docs
part: "why-not-java"
description: "event, delegates, yield and LINQ, nullable reference types, extension members, expression trees, source generators, reified generics, static abstract members, and value types, each named with a forward reference to the chapter that develops it."
---

## Learning objectives

By the end of this chapter you can:

- Name ten C# language and runtime features that change a classic pattern's shape, and where each is developed.
- Tell a compiler-enforced feature (a nullable warning, a reified generic instantiation) from a library-level one built from ordinary types (LINQ, expression trees).
- Name what the compiler generates for an `event`: the backing delegate field and the `add`/`remove` accessors.
- Predict which patterns will look least like their 1994 catalog diagrams.

## Why this matters

Modern C# and C# written in the 1994 catalog's object-and-interface style look alike: braces, `interface`, `class`, generics in angle brackets. An engineer with the catalog's abstract-class-and-factory habits can write working C# without touching the features that change a pattern's shape. The table below names each feature, what it changes, and where it's put to work.

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

| Level | Features | Consequence |
|---|---|---|
| Library and runtime, present since .NET's first release or built from ordinary types | Delegates, expression trees (`System.Linq.Expressions`), reified generics | Can be approximated from ordinary types without dedicated syntax. |
| Dedicated compiler syntax, added across later C# versions | `event`, `yield return`, nullable annotations, `extension`, source generators, `static abstract`, `struct`/`readonly struct`/`Span<T>` | Exists only once the compiler recognizes it. |

### Delegates and the `event` keyword

The catalog broadcasts to unknown listeners through a subject that holds a list of single-method listener objects and loops over it. A `ConsignmentTracker` that reports cargo clearing customs declares an event instead:

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

From outside the declaring class, `+=` and `-=` are the only operations. Code there can't call `Invoke` or clear the list; the C# language specification states this as a rule for event accessors. One field replaces a listener interface, a registration method and a notification loop. A `Func<Consignment, bool>` and a `Func<Consignment, decimal>` are two instantiations of one generic delegate type, not two unrelated interfaces to look up. Strategy, Template Method and Command all use the same mechanism.

### `yield` and LINQ

The catalog's Iterator is a cursor class with a position field and `First`, `Next`, `IsDone` and `CurrentItem`. In a method returning `IEnumerable<T>`, `yield return` turns loops, conditionals and early returns into a compiler-generated state machine, as the C# language reference's iterators chapter documents. LINQ adds a query layer on the same interface: `history.Where(e => e.RecordedAt >= since).OrderBy(e => e.RecordedAt)` evaluates lazily as the caller enumerates, so a tracking feed composes several filters before any of them runs. `IAsyncEnumerable<T>` applies the same laziness to handling events arriving over time.

### Nullable reference types

C++ and Smalltalk couldn't say whether a reference might be absent, the gap Null Object filled. With nullable reference types on (they are, in `Directory.Build.props`), `string` means not null and `string?` means maybe. Flow analysis tracks the difference through a method body and warns when a `string?` reaches a `string` without a check ([nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)). The guarantee exists only at compile time: at runtime both are the same reference, with no wrapper to unwrap.

### Extension members

Adding a member to a type you don't own and can't subclass used to mean a static utility call such as `CarrierStatusUtils.Format(status)`, which moves formatting away from the type. Extension methods, since C# 3, give that call instance syntax. C# 14's `extension` blocks also add properties and static members, so a status type from a carrier's SDK can gain a read-only `FormattedEta` property from a file in your own codebase.

### Expression trees

A lambda assigned to `Func<Consignment, bool>` compiles to a method. Assigned to `Expression<Func<Consignment, bool>>`, the compiler builds an object graph describing the comparison and its operands instead of IL ([expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/)). A tariff rule, weight over 500 kilograms and a destination in a bonded zone, can then be inspected, logged or partially evaluated by a rule engine before it runs.

### Source generators

A source generator is a Roslyn component that runs during compilation, reads the syntax trees and emits more source into the same compilation as visible, debuggable `partial` classes, with no reflection cost at runtime ([source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)). A generator that reads a carrier manifest and emits one gateway adapter per carrier produces code you can open and step through, in place of a reflection-based factory resolved at runtime. Analyzers use the same compiler API to enforce a pattern instead of generating it.

### Reified generics and `static abstract` members

`List<int>` and `List<string>` are distinct runtime types, part of the CLR's design since .NET's first release. The JIT produces code specialized to `int`, so an `int` in a `List<int>` is never boxed. `static abstract` interface members build on that: an interface declares a static operator or factory, and a generic method constrained to it calls `T.Zero` or `a + b`, resolved per instantiation with no boxed wrapper. An `IRateable<TUnit>` written once against a generic-math interface runs specialized, unboxed code for both `Money` and `Weight`.

{% include excalidraw.html name="03-reification-pipeline" caption="Figure 3.2: Generic instantiation in C#: the compiler and CLR produce a distinct, specialized runtime type for each type argument, with no boxing of value-type elements." %}
<!-- Figure 3.2 spec: a single compile/runtime pipeline. Source declares List<int> and List<string>; the Roslyn compiler and CLR JIT produce two distinct specialized runtime types, each storing its element inline with no boxing. Label each pipeline stage (source, Roslyn compile, CLR JIT, specialized runtime types). No chapter numbers or internal codes in the drawing. -->

### Value types

A `readonly struct` such as `Money`, `Weight` or `SlotId` is copied and compared by value and stored inline in its container or on the stack, not on the heap. `Span<T>` references a slice of an array or of stack memory, such as one field inside an EDI segment, without copying or allocating. A pattern built on a small, frequently created value chooses that layout explicitly instead of defaulting to a heap object with reference comparison.

## For deeper coverage

- The Microsoft Learn pages linked above, and the C# language specification, are the starting points for each feature.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
