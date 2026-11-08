---
title: "What 1994 assumed, and what C# invalidated"
order: 2
layout: tutorial
sectionid: docs
part: "why-not-gof"
description: "The object-orientation assumptions the Gang of Four wrote under, and which of them C# has since turned into language features."
---

## Learning objectives

By the end of this chapter you can:

- Name the two capabilities the 1994 catalog's C++ and Smalltalk examples
  lacked, and the ceremony that works around them.
- Replace an Observer subject's subscriber list with an `event`.
- Replace a Strategy interface and its classes with a `Func<>` parameter.
- Replace most Null Objects with a nullable reference type.
- Decide whether a pattern dissolves into a language feature or keeps its
  structure.

## Why this matters

Asked to make a rate quote vary by customer, an engineer trained on the 1994
catalog writes an interface, one class per variation and a factory to pick one,
because that was the only type-safe way to parameterize behavior in the
languages the catalog used. In C# the requirement is a parameter typed
`Func<Consignment, Money>`. The interface-and-factory version adds four types
that a reviewer reads, tests cover and an AI coding assistant loads into context
before it can reason about a one-line change.

Some catalog entries, and some of the 1990s literature that followed, exist to
compensate for what their host languages couldn't do. C# does several of those
things directly. Each language claim below cites Microsoft Learn.

{% include excalidraw.html name="02-what-1994-assumed-and-csharp-invalidated" caption="Figure 2.1: Three gaps the 1994 object model left open, no first-class functions, no compiler-checked nullability, and hand-rolled subscriber lists, each now closed by a C# language feature." %}

## The object model the catalog assumed

The catalog's examples are C++ and Smalltalk. Both left two gaps.

| Gap | C++ and Smalltalk in 1994 | Workaround the catalog uses | C# feature |
|---|---|---|---|
| Behavior as a value | C++ had member function pointers, awkward to bind to an instance; lambdas arrived in C++11. Smalltalk had blocks, but the catalog's diagrams favor an explicit object graph. | Wrap the behavior in an object and pass the object. | Delegates, lambdas, `event` |
| Present versus possibly absent | Any C++ reference or Smalltalk variable could hold null or nil, and nothing told a caller which needed a check. | Return null and trust every caller, or return a do-nothing stand-in. | Nullable reference types |

Those two gaps account for a large share of the catalog's structural ceremony.
Three patterns show it.

## Observer: the event keyword absorbs the subscriber list

Observer lets one object announce that something happened without knowing who
listens. The catalog's `Subject` keeps a list of observers with `Attach`,
`Detach` and `Notify`, and each listener implements an `Observer` interface.

The `event` keyword declares a member backed by a delegate type. The compiler
generates the subscription list, the `+=` and `-=` operators, and the
invocation that reaches every current subscriber:

```csharp
public sealed class Consignment
{
    public event EventHandler<ConsignmentDelayedEventArgs>? Delayed;

    public void ReportDelay(TimeSpan by) =>
        Delayed?.Invoke(this, new ConsignmentDelayedEventArgs(by));
}

// A tracking dashboard subscribes without implementing an Observer interface.
consignment.Delayed += (sender, args) =>
    Console.WriteLine($"Consignment delayed by {args.By}");
```

Microsoft Learn's design goals for events are minimal coupling between source and
listener, simple subscription and unsubscription, and any number of subscribers
including zero ([Introduction to
events](https://learn.microsoft.com/en-us/dotnet/csharp/events-overview)). Those
are the properties Observer's Consequences section claims, delivered once by the
compiler.

The obligation Observer always carried remains: a subscriber that doesn't
unsubscribe stays referenced by the subject and can't be collected. The same
Microsoft Learn page names that cost.

## Strategy: a delegate variable replaces the interface hierarchy

Strategy lets a caller pick one algorithm from a family at runtime, without the
using code knowing which one it got. The catalog's shape is a `Strategy`
interface, one class per algorithm and a `Context` holding one.

In C# the method takes the algorithm as a value, typed `Func<TArgument, TResult>`
or `Action<TArgument>`:

```csharp
public static Money QuoteRate(Consignment consignment, Func<Consignment, Money> pricing) =>
    pricing(consignment);

Money standard  = QuoteRate(consignment, c => c.Weight.Kilograms * StandardRatePerKilogram);
Money expedited = QuoteRate(consignment, c => c.Weight.Kilograms * ExpeditedRatePerKilogram + ExpeditedSurcharge);
```

`Func<T,TResult>` encapsulates a method with one parameter that returns a
`TResult`, so a caller doesn't have to define its own delegate type
([`Func<T,TResult>` Delegate](https://learn.microsoft.com/en-us/dotnet/api/system.func-2),
Microsoft Learn). `Action<T>` is the void-returning counterpart
([`Action<T>` Delegate](https://learn.microsoft.com/en-us/dotnet/api/system.action-1),
Microsoft Learn). Microsoft Learn describes delegates as a language construct for
late binding, where the caller supplies a method that implements part of the
algorithm ([Introduction to delegates and
events](https://learn.microsoft.com/en-us/dotnet/csharp/delegates-overview)).

`QuoteRate` needs no `IRateStrategy`, no `StandardRateStrategy` or
`ExpeditedRateStrategy`, and no factory. Keep an interface-based Strategy when
the algorithm:

- carries its own state across calls;
- implements several related operations, not one;
- is resolved from a DI container under a key instead of chosen inline.

When the only requirement is that the caller picks the behavior, a delegate
value is the whole solution.

## Null Object: nullable reference types make the stand-in redundant

Null Object removes the null check at every call site by returning an object
that implements the interface and does nothing. Bobby Woolf documented it a few
years after the catalog, answering the same gap: no reference in C++ or
Smalltalk said whether it could be absent.

Nullable reference types put that in the signature, and the compiler enforces
the check:

```csharp
public interface ICarrierGateway
{
    Quote? TryGetQuote(Consignment consignment);
}

Quote? quote = gateway.TryGetQuote(consignment);
if (quote is not null)
{
    Book(quote);
}
```

Nullable reference types are a group of features that minimize the chance of a
[`NullReferenceException`](https://learn.microsoft.com/en-us/dotnet/api/system.nullreferenceexception).
For a variable of type `Quote?`, the compiler warns on a dereference that isn't
preceded by a check (Microsoft Learn, [Nullable reference
types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/nullable-reference-types)).
Null Object made every call site safe by handing it a harmless object. The
compiler now flags the unsafe call site before the program runs.

A no-op collaborator called unconditionally, such as a `NullAuditLog` in a test
double or a default configuration, still earns a real object, because the goal
there is removing a branch, not expressing absence. Nullable types remove the
common case: a Null Object written only because the language couldn't say "this
might be missing; check first."

## Did the force move, or did it stay?

Observer, Strategy and Null Object dissolve because the force each resolved is
now handled by a compiler-checked construct:

| Pattern | Force | Resolved by |
|---|---|---|
| Observer | Broadcast to unknown listeners | `event` |
| Strategy | Caller substitutes behavior | `Func<>`, `Action<>` |
| Null Object | Represent an absent value safely | `T?` with compiler warnings |

They weren't wrong in 1994. The gap they filled is gone.

That says nothing about patterns that organize several collaborating objects.
Composing a tree of consignments and shipments, managing the lifetime of an
expensive shared resource, or coordinating a multi-step clearance across
independent handlers are forces about the domain's shape, which no language
feature addresses.

For each classic pattern, read the force in its Consequences section and ask:

1. Is it a gap in what the language can express? Then it's a candidate to
   dissolve into the feature that closes the gap.
2. Is it about how collaborating objects in the domain relate? Then it keeps its
   shape, written in modern C#.

Composite answers the second way. Its force is letting a client treat a single
`Shipment` and a `ConsolidatedShipment` of several smaller ones through one
interface, so code that totals weight or counts line items doesn't care which it
has. That describes how consolidations relate to consignments. No delegate,
nullable annotation or other C# feature changes it, so Composite keeps its
original shape.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the source
  for the Observer and Strategy descriptions measured here against the
  language. Null Object followed a few years later, from the same
  object-oriented design-patterns community, and is held to the same test.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
