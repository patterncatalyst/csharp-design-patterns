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

- Name two capabilities the 1994 catalogue's C++ and Smalltalk examples
  didn't have, and explain which pieces of catalogue ceremony exist mainly to
  work around their absence.
- Cite the Microsoft Learn source for the `event` keyword and explain, with a
  short example, how it implements the publish-and-subscribe relationship
  Observer names.
- Cite the Microsoft Learn source for `Func<>` delegates and explain how a
  delegate value lets a caller supply interchangeable behavior without a
  Strategy interface and its concrete implementations.
- Cite the Microsoft Learn source for nullable reference types and explain
  why they remove most of the reason to write an explicit Null Object.
- State the test this book applies to each classic pattern to decide whether
  it has dissolved into a language feature or still needs its original
  structure.

## Why this matters

A C# engineer who learned design patterns from the 1994 catalogue carries
habits suited to the constraints of its era, not C#'s. Faced with "this
rate quote needs to vary by customer," that training reaches for an
interface, a concrete class per variation, and a factory to wire them
together, because that was the only type-safe way to parameterize behavior
in the languages the catalogue was written against. In C#, the same
requirement is a parameter typed `Func<Consignment, Money>`. Writing the
interface-and-factory version anyway isn't more correct. It's four extra
types that a reviewer has to read, a test suite has to cover, and an AI
coding assistant has to load into context before it can reason about a
one-line change, all to resolve a force the compiler already resolves for
free.

This chapter makes a narrow, checkable claim: specific entries in the 1994
catalogue, and in the object-oriented design-patterns literature that
followed it closely, exist to compensate for things their host languages
couldn't do, and C# can now do several of them directly. Sources back that
claim, not assertion, so every language-feature claim below cites Microsoft
Learn or a compilable example, the same discipline this book asks of every
original-research chapter it writes.

{% include excalidraw.html name="02-what-1994-assumed-and-csharp-invalidated" caption="Figure 2.1: Three 1994 assumptions (no first-class functions, no compiler-checked nullability, hand-rolled subscriber lists) mapped to the C# features that now cover them: delegates, nullable reference types, and the event keyword." %}

## The object model the catalogue assumed

The Gang of Four catalogue's examples run overwhelmingly in C++ and
Smalltalk, the two languages its authors used daily in 1994. Both left gaps
that a design pattern was a reasonable way to paper over.

Neither language gave a convenient, type-safe way to pass a piece of
behavior around as an ordinary value. C++ in 1994 had member function
pointers, which are notoriously awkward to bind to a specific instance and
carry no useful type information about what they do; lambda expressions
didn't arrive in C++ until the 2011 standard, seventeen years later.
Smalltalk had blocks, which are closures, but the catalogue's own diagrams
favor an explicit object graph (an abstract `Strategy` class, one concrete
subclass per algorithm, a `Context` that holds a reference to one) over a
block passed directly to a method. Whichever language a team was using, the
safe, portable way to let a caller supply interchangeable behavior was to
wrap that behavior in an object and pass the object, because the language
gave you no cheaper way to say "here is some code, call it later."

Neither language's type system distinguished a reference that is always
present from one that might be absent. Every object reference in C++ and
every variable in Smalltalk could silently hold a null or nil value, and
nothing in the compiler or the type checker told a caller which references
needed a null check and which didn't. A method that sometimes had nothing
useful to return had exactly two options: return null and trust every caller
to check for it, or return a stand-in object that implements the same
interface but does nothing, so every caller can treat the absent case
exactly like the present one without a conditional.

Those two gaps, no cheap way to pass behavior as data and no type-level way
to say a reference cannot be null, explain a disproportionate share of the
catalogue's structural ceremony. C# closes both. What follows walks through
three patterns whose core forces are resolved by a language feature built
for exactly that gap.

## Observer: the event keyword absorbs the subscriber list

Observer's job is to let one object broadcast that something happened
without knowing, at compile time, who is listening. In the catalogue's
shape, a `Subject` base class hand-rolls a list of observers and `Attach`,
`Detach`, and `Notify` methods to manage it, and each listener implements an
`Observer` interface so the subject can call it generically.

C# builds that exact shape into the language. The `event` keyword declares a
member backed by a delegate type, and the compiler generates the
subscription list, the `+=` and `-=` operators to join and leave it, and the
null-safe invocation pattern to broadcast to every current subscriber:

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

Microsoft Learn's own framing of this feature could be describing Observer's
intent directly: events are "a way for an object to broadcast ... that
something happened," and any other component "can subscribe to the event,
and be notified when an event is raised" (Microsoft Learn, [Introduction to
events](https://learn.microsoft.com/en-us/dotnet/csharp/events-overview)).
The design goals Microsoft Learn lists for the feature, minimal coupling
between the source and the listener, simple subscription and
unsubscription, and support for any number of subscribers including zero,
are the same three properties Observer's Consequences section claims. The
pattern and the language feature are solving the same problem; C# just
solves it once, in the compiler, instead of asking every team to re-solve it
with a hand-written subscriber list.

What C# doesn't remove is the discipline Observer always required: a
subscriber still has to unsubscribe when it's no longer interested, or the
subject keeps a live reference to it and the listener can't be collected.
The same Microsoft Learn page names that cost directly, alongside the
feature it describes.

## Strategy: a delegate variable replaces the interface hierarchy

Strategy's job is to let a caller supply one algorithm out of a family,
selected at runtime, without the code that uses the algorithm knowing which
one it got. The catalogue's shape is an abstract `Strategy` interface, one
concrete class per algorithm, and a `Context` that holds a reference to
whichever concrete strategy it was given.

In C#, a method that varies by the algorithm its caller wants can just take
that algorithm as a value, typed `Func<TArgument, TResult>` or
`Action<TArgument>`:

```csharp
public static Money QuoteRate(Consignment consignment, Func<Consignment, Money> pricing) =>
    pricing(consignment);

Money standard  = QuoteRate(consignment, c => c.Weight.Kilograms * StandardRatePerKilogram);
Money expedited = QuoteRate(consignment, c => c.Weight.Kilograms * ExpeditedRatePerKilogram + ExpeditedSurcharge);
```

`Func<T,TResult>` is a built-in generic delegate type that, per its
Microsoft Learn reference entry, "encapsulates a method that has one
parameter and returns a value of the type specified by the `TResult`
parameter," specifically so a caller "[does] not have to explicitly define a
delegate that encapsulates a method" ([`Func<T,TResult>`
Delegate](https://learn.microsoft.com/en-us/dotnet/api/system.func-2),
Microsoft Learn). `Action<T>` is its void-returning counterpart, for the
same shape of problem when the algorithm doesn't produce a value
([`Action<T>` Delegate](https://learn.microsoft.com/en-us/dotnet/api/system.action-1),
Microsoft Learn). Microsoft Learn's overview of delegates traces the same
design goal the catalogue states for Strategy almost verbatim: a "common
language construct" for "late binding," so the caller can supply "at least
one method that implements part of the algorithm" (Microsoft Learn,
[Introduction to delegates and
events](https://learn.microsoft.com/en-us/dotnet/csharp/delegates-overview)).

`QuoteRate` above has no `IRateStrategy` interface, no `StandardRateStrategy`
or `ExpeditedRateStrategy` class, and no factory to choose between them. The
caller passing a lambda at the call site is Strategy's force, substitutable
behavior chosen by the caller, resolved with a value instead of an object
graph. An interface-based Strategy is still the better choice when the
"algorithm" needs to carry its own state across calls, implement several
related operations instead of one, or be resolved by a DI container under a
key rather than chosen inline; those are different forces, and this book's
Strategy chapter covers them on their own terms. The teaser here stays
narrow on one force only: when the sole requirement is "the caller picks the
behavior," a delegate value is the whole solution.

## Null Object: nullable reference types make the stand-in redundant

Null Object's job is to avoid an "if this is null" check at every call site
by handing back an object that implements the expected interface but does
nothing, so the absent case and the present case can be handled with the
same code path. Bobby Woolf documented it a few years after the Gang of Four
catalogue, in the same wave of 1990s object-oriented design-patterns
literature, answering the same underlying gap: neither C++ nor Smalltalk
let the type system distinguish "this reference is always present" from
"this reference might be absent"; every reference was a potential null, so
every call site that didn't control the object's creation had to guess
whether a check was needed.

C#'s nullable reference types put that distinction directly in the type
signature. A method that sometimes has nothing to return says so in its
return type, and the compiler, not convention, enforces the check:

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

Microsoft Learn describes nullable reference types as "a group of features
that minimize the chance your code throws
[`NullReferenceException`](https://learn.microsoft.com/en-us/dotnet/api/system.nullreferenceexception)":
a non-nullable type like `Quote` means "every person must have both"
properties filled in by design, and a nullable type like `Quote?` means the
compiler "warns if you dereference the variable without first checking it"
(Microsoft Learn, [Nullable reference
types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/nullable-reference-types)).
That warning is the mechanism Null Object existed to make unnecessary by a
different route: instead of guaranteeing every call site behaves safely by
handing it a safe do-nothing object, the compiler now tells a call site
directly when it's handling a possibly-absent value unsafely, before the
program runs.

This doesn't retire every use of Null Object. A collaborator that wants a
no-op implementation it can call unconditionally, say, a
`NullAuditLog` used in a test double or a default configuration, still
benefits from a real object that satisfies the interface and does nothing,
because the goal there is eliminating a branch, not expressing possible
absence. What nullable reference types remove is the far more common case:
a Null Object written only because the language gave no better way to say
"this might not be here, check before you use it."

## The test: did the force move, or did it stay?

Observer, Strategy, and Null Object dissolve under this reading because, in
each case, the force the pattern resolved, broadcasting an event to unknown
listeners, substituting behavior at a call site, representing an absent
value safely, is now resolved by a construct built into the language and
checked by the compiler. None of the three disappear because they were
wrong in 1994. They disappear because the gap they were invented to fill
isn't there anymore in C#.

That's a narrow kind of invalidation, and it's worth being precise about
what it doesn't claim. It doesn't claim the catalogue's structural patterns,
the ones that organize relationships among several collaborating objects
rather than substitute a single piece of behavior, are obsolete. Composing
a tree of consignments and shipments, managing the lifetime of an expensive
shared resource, or coordinating a multi-step clearance process across
several independent handlers are forces about the shape of a domain, not
about whether the host language has first-class functions or nullable
annotations. A language feature doesn't resolve a force it was never built
to address.

The test, then, is specific: for a given classic pattern, ask what force it
names in its Consequences section, and ask whether that force is a gap in
what the language itself can express, or a gap in how several collaborating
objects in your domain relate to each other. The first kind is a candidate
to dissolve into the feature that closes the gap. The second kind keeps
earning its original shape, written in idiomatic modern C#, for as long as
the domain keeps presenting that shape of problem.

Composite is a useful check on the test, because it fails it in the opposite
direction from Observer and Strategy. Composite's force is letting a client
treat a single `Shipment` and a `ConsolidatedShipment` made of several
smaller ones through the same interface, so code that totals weight or
counts line items doesn't need to know which one it has. That's a statement
about how consignments and consolidations relate to each other in a
shipping company's domain. No delegate type, no nullable annotation, and no
other C# 14 feature changes what a consolidated shipment is or how its parts
compose. Composite keeps its original shape in this book, in modern C#
syntax, because the test gives a different answer than it gave for Observer
and Strategy, not because the test was skipped for it.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the source
  for the Observer and Strategy descriptions this chapter measures against
  the language. Null Object followed a few years later, from the same
  object-oriented design-patterns community, and is held to the same test.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
