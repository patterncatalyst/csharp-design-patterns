---
title: "Pattern-to-force quick reference"
order: 51
layout: tutorial
sectionid: docs
part: appendices
appendix: "E"
description: "A quick reference mapping every pattern, principle, and idiom in the book to the forces it answers."
---

## Learning objectives

- Look up, for any named pattern, principle, or idiom in this book, the force
  or forces it exists to answer.
- Explain "force" in the decision-under-pressure sense this book uses it:
  the concrete pressure a design choice resolves, not a label to apply for
  its own sake.
- Find, for a force you are feeling right now ("I need to add behavior
  without subclassing," "I need exactly one instance, created safely under
  concurrent access"), the pattern and the modern C# 14 / .NET 10 feature
  that answers it.
- Judge whether a 1994 pattern still earns its own class hierarchy in
  current C#, or whether a language or runtime feature now answers the same
  force directly.

## Why this matters

A pattern name is a shorthand for a force, not a goal. The Gang of Four
catalogued Strategy, Observer, and Singleton because 1994 object-oriented
languages left programmers without interfaces-as-first-class-types,
closures, or a managed runtime, so a recurring pressure (vary an algorithm,
notify many listeners, guarantee one instance under concurrency) had no
direct language answer and needed a structural workaround. Reaching for a
pattern by name first, then hunting for a problem it fits, runs the
decision backward and tends to produce indirection nobody asked for. This
appendix inverts the lookup: it lists the forces this book's patterns,
principles, and idioms answer, and the specific C# 14 / .NET 10 feature
each one reaches for today. Some forces still need the original structural
answer. Others are now answered directly by the platform, a delegate
instead of a Strategy hierarchy, a record `with` expression instead of
`ICloneable`, a `Channel<T>` instead of a hand-rolled Active Object queue.
Knowing which is which, for a specific force, is the point of the book's
temporal thesis: not that the catalogue is wrong, but that fewer of its
1994 assumptions hold in C# 14 than they did in the language the catalogue
was written against.

## How to read this table

Each row names one pattern, principle, or idiom, the area of the book
where it is worked through, the force or forces that justify reaching for
it, and the modern C# construct that answers that force today. "Forces it
answers" is written as the pressure you would be feeling, not an abstract
category, so it doubles as a search key: if you are feeling a pressure
that matches a row, that row's pattern and modern C# note are your
starting point. Rows are grouped the way the Gang of Four grouped its own
catalogue, creational, structural, and behavioral, with the book's
principles, platform primitives, compiler-level idioms, async and
concurrency idioms, and process-level idioms (shortcuts, legacy code,
putting it together) grouped separately around them. A pattern that spans
more than one force, Decorator answers both "add responsibility without
subclassing" and "compose several responsibilities independently," gets
one row with both forces named; a chapter that covers more than one
pattern, Factory Method and Abstract Factory, gets one row per pattern.

## Reading the forces, before the catalogue

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Forces as the unit of analysis | Patterns as decisions under forces | comparing competing designs before committing to a pattern name; avoiding cargo-cult pattern selection | read this table's Forces column before reaching for a GoF name |
| 1994 assumptions audit | What 1994 assumed, and what C# invalidated | single-inheritance-only dispatch, no managed garbage collection, no first-class interfaces, no closures | GC, interfaces as first-class types, lambdas and closures, and generics remove several of the original forces outright |
| The divergence map | The divergence map | wayfinding across which patterns still apply unchanged and which collapse into a single language feature | use as an index: cross-reference a pattern name before reading its full chapter |
| Shared naming vocabulary | Naming patterns for teams and AI assistants | team communication, code-review shorthand, consistent prompts for AI coding assistants | name the pattern in identifiers and XML doc comments so humans and AI tools share one vocabulary |

## Principles and dependency injection

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Single Responsibility Principle (SRP) | the SRP/OCP/LSP chapter | one reason to change per collaborator; avoiding a class that absorbs every new concern | partition by responsibility into focused types; primary constructors keep each type's one dependency set explicit |
| Open/Closed Principle (OCP) | the SRP/OCP/LSP chapter | extending behavior without editing code already proven correct | a pipeline of small delegate- or interface-typed strategies; pattern matching over a closed, enumerable set of variations |
| Liskov Substitution Principle (LSP) | the SRP/OCP/LSP chapter | substitutability of any implementation behind a shared contract | record value equality and `with` make a substitutable value contract explicit; interface default members hold every substitute to the same contract |
| Interface Segregation Principle (ISP) | the ISP/DIP/DI chapter | forcing a consumer to depend on members it never calls | narrow, role-specific interfaces instead of one wide interface; explicit interface implementation to hide irrelevant members |
| Dependency Inversion Principle (DIP) | the ISP/DIP/DI chapter | high-level code must not depend on low-level implementation detail | depend on an interface or delegate type, never a concrete class; constructor injection via primary constructors |
| Dependency Injection (built-in container) | the ISP/DIP/DI chapter | runtime selection and lifetime control of a dependency without editing its callers | `Microsoft.Extensions.DependencyInjection`, keyed services for named variants, scoped/singleton/transient lifetimes |

## Platform primitives

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Dispose pattern | Dispose and IAsyncDisposable | deterministic release of a scarce unmanaged or async resource | `IDisposable` and `IAsyncDisposable`, `using` declarations, `await using` |
| Value types: struct, readonly struct, Span&lt;T&gt; | value types: struct, readonly struct, Span&lt;T&gt; | avoiding heap allocation and unwanted aliasing for small, frequently copied data | `readonly struct` for immutable value semantics; `Span<T>`/`ReadOnlySpan<T>` for allocation-free slicing |
| NRTs, records, init/required/with | NRTs, records, and init/required/with | enforcing invariants at compile time instead of runtime null checks or defensive copies | nullable reference types, record types with value equality, init-only and required members, non-destructive `with` |
| Test-double taxonomy | Proving it works: xUnit and NSubstitute | isolating a unit from its collaborators while keeping a test focused on behavior | xUnit v3 fact and theory tests, NSubstitute substitutes standing in for stubs, mocks, and spies |

## Creational patterns

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Factory Method | Factory Method and Abstract Factory | deferring which concrete type gets created to a subclass or a delegate | a factory delegate (`Func<T>`) or a keyed DI registration instead of a factory class hierarchy |
| Abstract Factory | Factory Method and Abstract Factory | producing a family of related objects that must stay consistent with each other | an interface per family, with keyed services resolving the whole family from one registration |
| Builder | Builder, required members, and Prototype with with | assembling a complex object step by step while keeping every intermediate state valid | required members plus object initializers; a fluent builder only when the steps have a fixed order |
| Prototype | Builder, required members, and Prototype with with | cloning an existing instance instead of re-deriving its configuration from scratch | record `with` expressions for a non-destructive copy; `ICloneable` is rarely needed once `with` exists |
| Singleton | Singleton, Lazy&lt;T&gt;, and DI lifetimes | exactly one instance, created on first use, safe under concurrent access | `Lazy<T>` for thread-safe lazy initialization, or a DI singleton lifetime instead of a static field |

## Structural patterns

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Adapter | Adapter, Facade, and Bridge | reconciling two incompatible interfaces without changing either one | a thin wrapper type implementing the target interface; an extension method for lightweight adaptation |
| Facade | Adapter, Facade, and Bridge | hiding subsystem complexity behind one simple entry point | a single service class composing several internal collaborators via constructor injection |
| Bridge | Adapter, Facade, and Bridge | decoupling an abstraction from its implementation so each can vary independently | an interface-typed field resolved through DI instead of a parallel inheritance hierarchy |
| Decorator | Decorator and Proxy | adding responsibility to an object without subclassing every combination | wrapping the same interface, chained through DI registration order, or pipeline-style delegate composition |
| Proxy | Decorator and Proxy | controlling access to an object: lazy loading, a remote call, a permission check | `Lazy<T>` for a virtual proxy; a source-generated interceptor for cross-cutting access control |
| Composite | Composite and Flyweight | treating a single object and a group of objects through one uniform interface | a shared interface over `IEnumerable<T>` children, walked with recursive LINQ instead of hand-rolled recursion |
| Flyweight | Composite and Flyweight | sharing immutable state across many logical instances to cut memory pressure | `readonly struct` instances, string interning, `FrozenDictionary` for a shared immutable lookup |

## Behavioral patterns

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Strategy | Strategy: delegates, interfaces, and keyed services | swapping an algorithm at runtime without an if/else ladder | a `Func<>` delegate parameter, or an interface plus keyed DI resolution for named strategies |
| Template Method | Template Method and the delegate alternative | fixing an algorithm's skeleton while letting one step vary | a delegate-typed hook parameter instead of a protected abstract method and a subclass |
| State | State and finite state machines | behavior that changes with an object's internal state, without a wall of conditionals | a state record or enum matched with a switch expression instead of a state class hierarchy |
| Command | Command, undo, and Task-returning commands | queuing, logging, or undoing an action as a first-class object | a record command plus a `Func<Task>` delegate; a stack of records for undo instead of a memento per command |
| Observer | Observer: the event keyword | notifying many interested parties of a change without coupling the subject to them | the built-in `event` keyword and `EventHandler<T>`, or `IObservable<T>` for a composable push sequence |
| Mediator | Mediator and Chain of Responsibility | decoupling a set of colleagues that would otherwise reference each other directly | a mediator service injected via DI, or channel-based message passing for the same decoupling |
| Chain of Responsibility | Mediator and Chain of Responsibility | passing a request along a chain of handlers until one of them handles it | an `IEnumerable<IHandler>` resolved from DI and walked in order, or middleware-style delegate chaining |
| Iterator | Iterator: yield return, LINQ, and IAsyncEnumerable | traversing a collection without exposing its internal representation | `yield return` for a lazy sequence, LINQ for composable traversal, `IAsyncEnumerable<T>` for an async stream |
| Visitor | Visitor, Memento, and Interpreter | adding a new operation over a closed set of types without editing every type | pattern matching over a sealed hierarchy; no double-dispatch `Accept`/`Visit` machinery needed |
| Memento | Visitor, Memento, and Interpreter | capturing and restoring an object's internal state without breaking encapsulation | a record snapshot plus `with`; init-only properties keep the snapshot immutable |
| Interpreter | Visitor, Memento, and Interpreter | evaluating sentences in a small domain-specific language | expression trees (`Expression<Func<T>>`) built and evaluated at runtime |

## Compiler-level idioms

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Extension methods and C# 14 extension members | extension methods and C# 14 extension members | adding behavior to a type you do not own, without subclassing or wrapping it | classic extension methods plus C# 14 extension members for extension properties and static members |
| Expression trees | expression trees: code as data | representing code as inspectable, transformable data instead of executing it directly | `Expression<Func<T>>` trees, compiled with `.Compile()`, consumed by LINQ providers and rules engines |
| Source generators | source generators: patterns at build time | generating boilerplate, mapping, serialization, DI wiring, at compile time instead of runtime reflection | incremental source generators (`IIncrementalGenerator`) emitting partial classes |
| Roslyn analyzers | Roslyn analyzers: patterns enforced at compile time | catching pattern misuse or a missing convention at compile time instead of in review | a custom `DiagnosticAnalyzer` paired with a `CodeFixProvider` |
| static abstract members and generic math | static abstract members and generic math | writing one generic algorithm over any type that satisfies an operator contract | static abstract interface members, the `INumber<T>` hierarchy for generic math |

## Async surface idioms

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Options pattern | the Options pattern | validating and strongly typing configuration instead of passing loose strings or dictionaries | `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>` with an attached validator |
| Tester-Doer and the Result debate | Tester-Doer and the Result debate | avoiding a race between checking a precondition and acting on it; reporting failure without throwing | a `Result<T>`-style record instead of a try-method plus a boolean, where the check-then-act race matters |
| TAP async conventions | TAP and the async conventions | a consistent shape for asynchronous operations across a codebase | the Task-based Asynchronous Pattern: an `Async` suffix, a `CancellationToken` parameter, a `Task`/`Task<T>`/`ValueTask<T>` return |

## Concurrency idioms

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Stackless async model | why .NET chose stackless async | running many concurrent operations without one OS thread per operation | the compiler-generated async state machine; `await` suspends without blocking a thread |
| Thread-safe creation | thread-safe creation | initializing a shared resource exactly once under concurrent first access | `Lazy<T>` with a thread-safety mode, or `Interlocked.CompareExchange` for a lock-free check |
| Monitor Object | Monitor Object and Active Object on Channel&lt;T&gt; | serializing access to an object's state from multiple threads | the `lock` statement over a private object, or `Monitor` directly for a timeout or try-enter |
| Active Object | Monitor Object and Active Object on Channel&lt;T&gt; | decoupling method invocation from method execution across threads | a `Channel<T>` as the request queue, drained by one dedicated consumer loop |
| Producer-Consumer and pipelines | Producer-Consumer and pipelines as patterns | handing work between stages running at different throughput, without unbounded memory growth | `System.Threading.Channels`, a bounded `Channel<T>` wiring multiple pipeline stages |
| Pools: object, connection, ArrayPool | Pools: object, connection, and ArrayPool&lt;T&gt; | reusing a scarce or expensive-to-create resource instead of allocating it repeatedly | `ObjectPool<T>` from `Microsoft.Extensions.ObjectPool`, `ArrayPool<T>.Shared` for buffer reuse |
| Classic GoF under concurrency | classic GoF under concurrency | revisiting Singleton, Observer, and Command once multiple threads touch them concurrently | a thread-safe `Lazy<T>` Singleton, thread-safe event invocation, a `Channel`-backed Command queue |

## Shortcuts, anti-patterns, and legacy code

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Shortcut patterns and dispatch tables | shortcut patterns | picking behavior from a small, known set of keys without a class per case | a `Dictionary<TKey, Func<>>` dispatch table, or a switch expression over a closed set |
| Anti-pattern awareness | anti-patterns and pattern misuse | recognizing when a pattern adds indirection without answering a live force | match the candidate pattern back against this table's Forces column before applying it |
| Seams | seams in C#: non-virtual by default | finding a point to substitute behavior in code that was not written with testing in mind | an interface extracted at the seam, since C# members are non-virtual by default and sealing is common |
| Dependency-breaking techniques | dependency-breaking techniques for C# 14 | isolating a unit for testing without a large up-front rewrite | interface extraction, extract-and-override, wrapping a static call behind an injectable interface |
| Fowler vs Feathers | Fowler vs Feathers, contrasted and combined | choosing between a refactoring move and a characterization-test-first legacy technique | apply Feathers' seams to get a test in place, then Fowler's catalogue once coverage exists |
| Refactor or rewrite | refactor or rewrite | deciding whether to incrementally improve existing code or replace it outright | a cost-and-risk comparison; a strangler-fig approach when a rewrite is justified |

## Putting it together

| Pattern / principle / idiom | Where it appears | Forces it answers | Modern C# note |
| --- | --- | --- | --- |
| Composing multiple patterns in one system | final project | keeping several patterns from conflicting when they meet in the same codebase | a DI container wiring Strategy, Factory, and Decorator implementations together through one composition root |
| Pattern vocabulary as AI context | working with AI and LLM tools | giving an AI coding assistant enough shared vocabulary to suggest the right pattern, not just the nearest-named one | name patterns explicitly in code and prompts; review AI-suggested code against this table's Forces column |

## For deeper coverage

- Erich Gamma, Richard Helm, Ralph Johnson, John Vlissides, [*Design
  Patterns: Elements of Reusable Object-Oriented
  Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480/9780321700698)
  (Addison-Wesley, 1994): the catalogue this table maps against, pattern by
  pattern.
- Robert C. Martin, [*Agile Software Development: Principles, Patterns, and
  Practices*](https://www.pearson.com/en-gb/subject-catalog/p/agile-software-development-principles-patterns-and-practices-pearson-new-international-edition/P200000004932/9781292038360)
  (Prentice Hall, 2002): the source of the five SOLID principles in this
  table's principles rows.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
