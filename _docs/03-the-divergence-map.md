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
- Tell a compiler-enforced divergence (a nullable reference type warning, a reified generic instantiation) apart from a library-level one built out of ordinary types (LINQ, expression trees).
- Explain why a C# delegate and a Java single-abstract-method interface solve the same callback problem with different runtime machinery.
- Read a Java listener registration next to its C# event equivalent and point out what the C# compiler generates that the Java compiler does not.
- Predict, before reading them, which pattern chapters in this book will look least like their Java equivalents, and why.

## Why this matters

A C# source file and a Java source file read alike at a glance. Braces, semicolons, `interface`, generics in angle brackets, a `class` keyword doing the same job in both: the family resemblance is real, and it is also the trap. A developer who spent a decade writing Java functional interfaces and anonymous inner classes can compile working C# without hitting a single feature Java lacks, and come away convinced the two languages are the same vocabulary wearing different keywords. They are not. C# has shipped more than a decade of language and runtime additions with no Java counterpart, and several of the pattern chapters in this book exist in their current shape because of them, not despite them.

This chapter is a map, not a tutorial. Each entry below names a feature, states what it changes, and points at the chapter that puts it to work. Read it once before the pattern chapters start, and the "why does this look different from the Java book" moments later stop being surprises. They become entries already filed under a name.

{% include excalidraw.html name="03-the-divergence-map" caption="Figure 3.1: Ten C#-only language and runtime features, each mapped to the chapter that puts it to work." %}

## Ten divergences, briefly

| Divergence | What changes | Developed in |
|---|---|---|
| The `event` keyword | Compiler-generated `add`/`remove` accessors restrict subscription to `+=`/`-=` from outside the declaring type | "Observer: the event keyword" |
| Delegates | A method reference is a first-class runtime type with its own multicast invocation list, not an anonymous class implementing a single-method interface | "Strategy: delegates, interfaces, and keyed services"; "Template Method and the delegate alternative" |
| `yield` and LINQ | An iterator block compiles to a hidden state machine; LINQ turns `IEnumerable<T>` into a lazily evaluated query language | "Iterator: yield return, LINQ, and IAsyncEnumerable" |
| Nullable reference types | `string?` versus `string` is a compile-time annotation tracked by flow analysis, with no runtime wrapper | "NRTs, records, and init/required/with" |
| Extension members (C# 14) | An `extension` block can add properties and static members to a type it does not own, not only instance methods | "Extension methods and C# 14 extension members" |
| Expression trees | `Expression<Func<T, bool>>` captures code as an inspectable object graph instead of compiling it to IL | "Expression trees: code as data" |
| Source generators | A Roslyn component runs during compilation and injects visible `partial` classes, with no reflection cost at runtime | "Source generators: patterns at build time"; "Roslyn analyzers: patterns enforced at compile time" |
| Reified generics | `List<int>` and `List<string>` are distinct runtime types with their own specialized code, never boxed or erased to a shared class | "static abstract members and generic math" |
| `static abstract` interface members | An interface can require a static operator or factory method, so generic code can call `T.Zero` or `a + b` for any `T` that implements it | "static abstract members and generic math" |
| Value types | `struct`, `readonly struct`, and `Span<T>` let a user-defined type live inline on the stack, not limited to a fixed set of primitives | "Value types: struct, readonly struct, and Span<T>" |

Three of these ten live entirely in library and runtime design, not new syntax: delegates are an ordinary type the CLR has always had, expression trees are an ordinary class graph (`System.Linq.Expressions`) the compiler happens to build instead of emitting IL, and reified generics are a CLR design decision from .NET's first release, not a recent addition. The other seven depend on dedicated compiler syntax: `event`, `yield return`, nullable annotations, `extension`, source generators, `static abstract`, and `struct`/`readonly struct`/`Span<T>` all need the compiler to recognize a keyword or attribute that Java's grammar has no slot for. The distinction matters for a reader deciding how deep a feature goes: a library-level divergence can sometimes be approximated in Java with enough extra code, while a syntax-level one can't be approximated at all, only worked around.

### Delegates and the `event` keyword

In Java, a callback is an object: a class implementing a single-abstract-method interface, instantiated as a lambda or an anonymous inner class, held in a list, and invoked by iterating that list by hand. C# has the same need, a `ConsignmentTracker` that notifies interested code as cargo moves and eventually clears customs, but the compiler does more of the work. A delegate type is a runtime type in its own right with a built-in invocation list; declaring a member `event Action<string, ConsignmentStatus>? StatusChanged` gives external code exactly two operations, `+=` and `-=`, and the compiler generates the `add`/`remove` accessors that enforce it. Code outside the declaring class cannot call `Invoke` directly or clear the list, a restriction the C# language specification's description of event accessors states as a compiler rule, not a convention a team has to remember to follow.

{% include codetabs.html langs="Java|C#" %}
```java
public interface ConsignmentStatusListener {
    void onStatusChanged(String trackingNumber, ConsignmentStatus status);
}

public class ConsignmentTracker {
    private final List<ConsignmentStatusListener> listeners = new ArrayList<>();

    public void addListener(ConsignmentStatusListener listener) {
        listeners.add(listener);
    }

    public void markCleared(String trackingNumber) {
        for (ConsignmentStatusListener listener : listeners) {
            listener.onStatusChanged(trackingNumber, ConsignmentStatus.CLEARED);
        }
    }
}
```
```csharp
public class ConsignmentTracker
{
    public event Action<string, ConsignmentStatus>? StatusChanged;

    public void MarkCleared(string trackingNumber) =>
        StatusChanged?.Invoke(trackingNumber, ConsignmentStatus.Cleared);
}
```

The Java version needs an interface, a registration method, and a manual loop; the C# version needs one field declaration. Delegates also compose where Java's single-method interfaces don't: a `Func<Consignment, bool>` and a `Func<Consignment, decimal>` are two instantiations of the same generic delegate type, not two unrelated interfaces a reader has to look up separately. "Strategy: delegates, interfaces, and keyed services" builds a strategy entirely out of delegate fields and compares it against the interface-based version a Java-trained reader expects; "Template Method and the delegate alternative" and "Command, undo, and Task-returning commands" each lean on the same mechanism for a different GoF shape.

### `yield` and LINQ

Java's `Iterable` requires a hand-written `Iterator` with a `hasNext`/`next` pair tracking position in a mutable field. C#'s `yield return`, inside a method that returns `IEnumerable<T>`, turns ordinary control flow, loops, conditionals, early returns, into a compiler-generated state machine; a method that pages through a consignment's tracking history reads like a description of the sequence, not an implementation of an iterator protocol, a transformation the C# language reference's chapter on iterators documents in detail. LINQ builds on the same `IEnumerable<T>` to add a declarative query layer: `history.Where(e => e.RecordedAt >= since).OrderBy(e => e.RecordedAt)` evaluates lazily, one element at a time, as the caller enumerates it. Java's Stream API looks similar on the page but commits to a terminal operation up front and can't resume; C#'s sequences stay lazy until something iterates them, which changes how a tracking feed can compose filters before running any of them. "Iterator: yield return, LINQ, and IAsyncEnumerable" covers all three together, including the async form that applies the same laziness to a live feed of handling events arriving over time.

### Nullable reference types

Java has no notion of a non-nullable reference type; `Optional<T>` wraps a value at the library level, and a plain `String` field offers no static guarantee against `null`. C#'s nullable reference types, enabled project-wide in this book's `Directory.Build.props`, work the other way: `string` means not null, `string?` means it might be, and the compiler's flow analysis, described in Microsoft's [nullable reference types documentation](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references), tracks which is which through a method body, warning when a `string?` reaches a place expecting `string` without a preceding check. The guarantee lives entirely in the type checker and the warnings it raises; a `string?` and a `string` are the same reference at runtime, with no wrapper type a caller has to unwrap. "NRTs, records, and init/required/with" works through what this means for a `Consignment` record's constructor, and how a `with` expression produces an amended copy without ever reopening the question of whether a field might be null.

### Extension members

Java's answer to "add a method to a type you don't own" is a static utility method, called awkwardly as `CarrierStatus.formatted(status)` instead of `status.formatted()`. C# has had extension methods since C# 3, called with instance syntax; C# 14 widens the mechanism to extension members, letting an `extension` block add properties and static members to a type as well as instance methods. A status type defined inside a carrier's own SDK, one this book's code doesn't own and can't subclass, can still gain a read-only `FormattedEta` property from a separate file in this codebase. "Extension methods and C# 14 extension members" works through both forms against exactly that problem: extending a carrier SDK type the book never gets to edit.

### Expression trees

A C# lambda assigned to `Func<Consignment, bool>` compiles straight to a method, the same as a Java lambda. Assigned to `Expression<Func<Consignment, bool>>` instead, the compiler builds a data structure describing the lambda's logic, which comparison, which operands, rather than compiling it to IL at all, a distinction Microsoft's [expression trees documentation](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/) spells out as two different compiler targets for the same syntax. A tariff rule, weight over 500 kilograms and a destination in a bonded zone, represented this way can be inspected, logged, or partially evaluated by a rule engine before anything runs, instead of only ever being executed as opaque code. Java has no equivalent in the language; a comparable library has to parse bytecode or require callers to build a query object by hand. "Expression trees: code as data" builds the tariff rule engine against this mechanism to make it concrete.

### Source generators

Java's long-standing answer to "generate code at build time" is annotation processing, and its most common consumer, Lombok, rewrites compiled bytecode in ways the Java compiler was never designed to expose, which is why it needs special IDE and build-tool support to work at all. A C# source generator is an ordinary Roslyn component that runs during compilation, reads the syntax trees the compiler already produced, and emits additional source the same compilation includes, as visible, debuggable `partial` classes with zero reflection cost at runtime, a pipeline Microsoft's [source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) documents end to end. A generator that reads a declarative carrier manifest and emits one gateway adapter per carrier runs on every build and produces code a reader can open and step through, rather than a reflection-based factory resolved only at runtime. "Source generators: patterns at build time" and "Roslyn analyzers: patterns enforced at compile time" cover generation and enforcement as two sides of the same compiler API.

### Reified generics and `static abstract` members

Java generics are erased: at runtime, `List<Integer>` and `List<String>` are both just `List`, and type parameters exist only for the compiler. A Java generic method that needs to create a `new T()` or switch behavior by `T` has to pass a `Class<T>` token alongside the type parameter, since the parameter itself is gone by the time the method runs, and a primitive type argument has to arrive boxed because the shared erased code only knows how to hold a reference. C# generics are reified: `List<int>` and `List<string>` are distinct runtime types, each with code specialized for its type argument, and an `int` never gets boxed just to satisfy the generic constraint, a guarantee the .NET generics documentation describes as part of the CLR's design. That difference is what makes `static abstract` interface members possible: an interface can declare a static abstract operator or factory method, and a generic method constrained to that interface can call `T.Zero` or `a + b` for any `T` that implements it, resolved per instantiation at compile time, with no token or boxed wrapper carried along to make the dispatch work. An `IRateable<TUnit>` written once against a generic-math interface can run specialized, unboxed code for a `Money` amount and a `Weight` value in the same compiled assembly, without the weight calculation paying for the money calculation's branches or vice versa. "static abstract members and generic math" covers both the reification that makes this possible and the generic math interfaces the base class library ships on top of it.

{% include excalidraw.html name="03-reification-vs-erasure" caption="Figure 3.2: Generic instantiation compared: C# compiles List<int> and List<string> to distinct specialized types, while the JVM erases List<Integer> and List<String> to one shared List class with boxed elements." %}
<!-- Figure 3.2 spec: two side-by-side compile/runtime pipelines. Left (C#): source declares List<int> and List<string>; compiler/JIT produce two distinct specialized runtime types, each storing its element inline, no boxing. Right (Java): source declares List<Integer> and List<String>; javac erases both to one List bytecode class at compile time; Integer is boxed on the heap; type parameters are gone by runtime. Label each side with its compiler/runtime name (Roslyn + CLR JIT vs javac + JVM erasure). No chapter numbers or internal codes in the drawing. -->

### Value types

Java's primitives (`int`, `double`, `boolean`) are a closed, built-in set; every other type, including a one-field wrapper around a container slot identifier, is a reference type living on the heap. C#'s `struct` lets any type opt into value semantics: `Money`, `Weight`, and `SlotId` can each be defined as a `readonly struct`, copied by value, compared by value by default, and allocated inline inside its containing object or on the stack instead of the heap. `Span<T>` goes further, giving a type that can reference a slice of an array, or of stack-allocated memory, such as one field inside an EDI segment, without copying it or allocating a new object. Java has no user-definable value type at all as of this book's printing, so a Java port of any pattern involving a small, frequently allocated value, a money amount, a weight, a slot identifier, has to accept heap allocation and reference comparison as the only option. "Value types: struct, readonly struct, and Span<T>" works through the tradeoffs this book's shipping-domain types make with that choice available.

None of these ten items is exotic; each ships in the box with .NET 10, and each gets exercised somewhere in this book's examples. The map doesn't try to rank them by importance or teach them in depth; both jobs belong to the chapters it points to. What it buys a Java-trained reader is a vocabulary to recognize unfamiliar C# on sight, instead of mistaking it for an unfamiliar library convention. The next chapter puts a related piece of that vocabulary to work for a different purpose: naming the design itself, not just the language feature it is built from.

## For deeper coverage

- Each chapter named in the table above cites the specific Microsoft Learn page or C# language specification section for its feature as it goes deeper; this chapter only names the starting points.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
