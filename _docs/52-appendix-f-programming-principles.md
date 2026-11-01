---
title: "Programming principles reviewed in code"
order: 52
layout: tutorial
sectionid: docs
part: appendices
appendix: "F"
description: "A code-first review of the wider programming-principle vocabulary beyond SOLID."
---

## Learning objectives

By the end of this appendix you can:

- Name eleven programming principles from outside SOLID (DRY, orthogonality,
  YAGNI, KISS, composition over inheritance, the Law of Demeter, fail-fast,
  the Robustness Principle, the Boy-Scout Rule, separation of concerns, and
  Brooks's Law) and state what each one asks of a design.
- Point at a concrete pattern or example in this book where a principle is
  embodied in working code, even where the surrounding chapter never names
  the principle.
- Trace a guard clause written as `ArgumentNullException.ThrowIfNull` back to
  fail-fast, and a source generator or analyzer back to DRY.
- Explain why composition over inheritance, not a rule from SOLID, is the
  reason Strategy, Decorator, and Bridge pass a collaborator in rather than
  extending a base class.
- Find the book behind each principle in the bibliography and read the
  chapter and verse this appendix only summarizes.

## Why this matters

The chapters in this book build toward SOLID and the Gang of Four catalogue
by name. Most of them never name a second, older vocabulary that the same
decisions rest on. The one exception is Single Responsibility, named
directly where the SRP/OCP/LSP chapter introduces it; every other term
below is never written into the chapters at all. The chapters teach the
pattern and the platform feature a reader needs at the point they need it,
not a checklist of principle names to recite.

This appendix supplies the missing vocabulary and points each term at a
real pattern or example already in the book, not the other way around. A
reader who already knows DRY or the Boy-Scout Rule from elsewhere will
recognize the decision the moment it is named. A reader meeting a term here
for the first time gets a working example to anchor it to, plus the book
that states the principle in full. Either way, the point of this appendix
is recognition, seeing that a design choice already made for a concrete
reason also has a name in the wider literature, not a new rule to apply on
top of what the chapters already do.

## DRY: don't repeat yourself

Thomas and Hunt state DRY as "every piece of knowledge must have a single,
unambiguous, authoritative representation within a system." The Template
Method pattern is a direct application: the fixed order of a customs
declaration's three steps, gather required fields, calculate duty, run
validation, is written once, in the template method itself, and every
destination country's rules fill in the steps without touching or
duplicating that order. A subclass or a delegate-record step set supplies
content; neither one re-states the sequence.

Source generators and analyzers take the same principle further, into the
build itself. A hand-maintained carrier registry needs an entry for every
carrier, a second piece of knowledge about carriers living next to the
`ICarrierGateway` implementations themselves. A `IIncrementalGenerator`
that reads those implementations and emits the registry keeps exactly one
representation, the implementations, and derives the rest at compile time.
The money-as-minor-units rule a Roslyn analyzer enforces is the same move
applied to a convention: instead of a comment on a base contract type that
a reviewer has to remember and re-check on every pull request, the rule
lives once, in the analyzer, and the compiler re-checks it on every build
without being asked.

## Orthogonality

An orthogonal design lets one part change without forcing a change
somewhere unrelated. Dependency injection is orthogonal by construction: a
service registered as `IPricingStrategy` can be swapped, a
`StandardTariff` for an `ExpressTariff`, without touching the
`StrategyRateQuoter` that consumes it, because the two sides communicate
only through the interface's shape. Keyed services push the same property
further: registering three pricing strategies under three string keys with
`AddKeyedSingleton` lets a caller change which implementation answers a
given key without the registration code for the other two keys moving at
all. Changing the express tariff's pricing algorithm has no effect on how
the standard or economy tariffs are registered or resolved, which is
exactly the independence orthogonality names.

## YAGNI: you aren't gonna need it

YAGNI says don't build a mechanism for a requirement that has not shown up
yet. The dispatch-table chapter is YAGNI applied as a positive choice: five
document kinds, each a pure rendering step with no state and no
dependencies of its own, are handled by one `FrozenDictionary` lookup built
once, rather than by an `IDocumentRenderer` interface, five implementing
classes, and a factory to wire them together. The chapter is explicit about
where that stops paying off: the moment a kind's own state or dependencies
outgrow a single rendering method, a dedicated handler type earns its
keep, but not before.

The anti-patterns chapter shows the same judgment made the other way, on
purpose, as the thing to avoid. A `Strategy` and an `Abstract Factory`
applied to a `IRateComponent` and `IRateComponentFactory` that only ever
have one real shape are the Gang of Four's own patterns built for a
flexibility the code has no present use for. Both chapters are the same
principle: build the abstraction the current requirement needs, not the one
a possible future requirement might.

## KISS and the cost of a pattern

Simplicity, in the sense McConnell uses it in his construction practices,
means preferring the solution a reader can hold in their head over the one
that is merely more general. The Strategy chapter's three levels, a bare
`Func<RateRequest, Money>` delegate, a named `IPricingStrategy` interface,
and a keyed-service registration, are not three implementations of the same
idea at the same cost. A delegate has no interface, no implementing class,
and no registration to read before understanding what it does; it is the
simplest form that still varies the pricing algorithm independently of the
caller. Reaching for the interface or the keyed-service form earns its
complexity only when more than one call site needs to select a strategy by
name or at runtime, not as a default. Asking "does this decision need the
pattern, or does a method suffice" before writing the more general form is
KISS applied to pattern selection specifically, and it is the same question
the shortcut-patterns and anti-patterns chapters answer for dispatch tables
and for Abstract Factory.

## Composition over inheritance

The Gang of Four state a preference for object composition over class
inheritance directly, and three of this book's chapters make it concrete.
Strategy composes a pricing algorithm into a `StrategyRateQuoter` as a
held reference rather than a base-class method a subclass overrides; the
quoter can hold any `IPricingStrategy` built anywhere, including one built
after the quoter's own type was compiled. Decorator composes behavior the
same way around a wrapped component instead of adding it through a deeper
subclass. Bridge composes a label kind and a label format as two
independently varying collaborators rather than a cross-product of
subclasses, one for every kind-format pair. In each case the alternative,
inheriting to get a behavior variant, would fix that variant at compile
time and tie it to one base class; composing it in as a passed-in value
keeps it swappable and keeps the varying pieces independent of each other.

## Law of Demeter and tell-don't-ask

A caller that only talks to its immediate collaborators, and never reaches
through one collaborator to another, is honoring what is usually called the
Law of Demeter. The Facade chapter embodies it directly: a label-printing
request that would otherwise chain through a carrier gateway, a document
renderer, and a manifest writer in sequence becomes one call into a
`ShippingFacade`, which holds the three subsystems and knows their order so
the caller does not have to. The Mediator chapter does the same work for
peer-to-peer coupling: two desks that would otherwise hold direct
references to each other to coordinate a handoff instead each talk only to
a shared mediator, which is the "tell, don't ask" half of the same idea,
telling the mediator what happened rather than asking each desk to query
the other's state first.

## Fail-fast

A fail-fast design detects a broken precondition at the boundary where it
first becomes observable rather than letting it propagate into a state that
is harder to diagnose. `ArgumentNullException.ThrowIfNull` is the simplest
instance of this in the book, and it appears at the top of methods across
patterns from Template Method and State through Iterator and the
static-abstract-members chapter: a `null` argument throws immediately,
before any step of the method runs, rather than surfacing later as a
`NullReferenceException` from deep inside an unrelated call. The
Tester-Doer-and-Result chapter extends the same instinct to a distinction
Clean Code and Code Complete both draw: a guard clause that throws because a
precondition was violated by the caller is not the same thing as a
`TryParse`-style doer or a `Result` value reporting an expected failure
the caller asked to be told about. The Dispose contract is fail-fast
applied to lifetime: a type correctly implementing the dispose pattern
needs no finalizer and no `Dispose(bool)` override when it holds no
unmanaged resource directly, because the failure mode a finalizer guards
against, forgetting to release something the runtime cannot reclaim on its
own, does not exist for that type, and pretending otherwise would hide that
fact rather than surface it.

## The Robustness Principle

Postel's Robustness Principle, be conservative in what you send and liberal
in what you accept, shows up wherever this book adapts an external shape
into an internal one. The Adapter chapter's three carriers each expose a
different request and response shape; the three `ICarrierGateway`
adapters accept whatever shape a given carrier sends and translate
it into the one `ShippingRequest`/`ShippingResponse` pair every other part
of the system relies on, rather than asking every caller to understand
three carrier-specific formats. The Options pattern applies the same
principle to configuration: `CarrierGatewayOptionsValidator`, run through
`ValidateOnStart()`, is liberal about collecting every validation problem
in a configuration section in one pass, but conservative about what the
bound `CarrierGatewayOptions` is allowed to represent once validation
passes, refusing to let a gateway start against a configuration nobody
checked. Parsing a raw value in the Tester-Doer chapter follows the same
shape: accept the input and report a precise failure, rather than letting
a malformed value reach a collaborator that was not written to expect it.

## The Boy-Scout Rule and doing one thing

Clean Code states the Boy-Scout Rule as leaving the code cleaner than you
found it, in whatever small way the current change already touches. The
legacy-code part of this book is that rule practiced at the seam level: a
sealed `LegacyRateEngine` with every collaborator wired inside it gets a
narrow, mechanical first move, extracting a seam an `IRateEngine` interface
can stand behind, before any larger refactor or rewrite decision gets made.
That first move does one thing, create a seam, and resists the temptation
to also fix the pricing logic, reorganize the method, or rename things
while the seam goes in. The refactor-or-rewrite
decision that follows depends on that discipline: a stable contract built
in one small, reviewable step is what lets a team build a rewrite alongside
a refactor and compare them against the same cases, instead of discovering
the next problem only after a larger change has already landed.

## Separation of concerns

Separation of concerns, the principle Martin builds much of *Agile Software
Development* around before stating SOLID on top of it, says a design should
keep distinct concerns, what varies for different reasons, in distinct
places. Single Responsibility is the SOLID-era, class-level statement of
exactly this idea: a class should have one reason to change, because the
collaborators that would make it change for a billing reason and the
collaborators that would make it change for a shipment-tracking reason are
different concerns, and mixing them into one class means a change driven by
either one risks the other. Layering a system into gateways, services, and
callers that each depend only downward follows the same principle one level
up: a change to how a carrier's rate is calculated stays inside the pricing
concern and never requires a change to the concern that renders a bill of
lading.

## Brooks's Law

Brooks's observation that adding people to a late software project makes it
later is a human and scheduling principle, not a code one, and it belongs
in a refactor-or-rewrite decision for exactly that reason. A rewrite that
is already behind schedule does not get faster by adding more engineers to
it: new hands need the existing rewrite's assumptions explained before they
can contribute to it, and that explaining time comes out of the same
calendar the rewrite was already short on. The stable contract a seam
produces does not fix that math, but it does mean a team is choosing
between refactor and rewrite with real evidence instead of a schedule
assumption, which is the only point at which Brooks's warning is worth
remembering: an estimate that depends on throwing people at a deadline is
the estimate to distrust first.

## For deeper coverage

- Thomas and Hunt, [*The Pragmatic Programmer: Your Journey to Mastery*](https://pragprog.com/titles/tpp20/the-pragmatic-programmer-20th-anniversary-edition/) (Addison-Wesley / Pragmatic Bookshelf, 2019): the source for DRY, orthogonality, YAGNI, the Law of Demeter, and the Robustness Principle reviewed above.
- Martin, [*Clean Code: A Handbook of Agile Software Craftsmanship*](https://www.pearson.com/en-us/subject-catalog/p/clean-code-a-handbook-of-agile-software-craftsmanship/P200000009044/9780132350884) (Prentice Hall, 2008): the source for the Boy-Scout Rule and the fail-fast guard-clause discipline reviewed above.
- McConnell, [*Code Complete: A Practical Handbook of Software Construction*](https://www.microsoftpressstore.com/store/code-complete-9780735619678) (Microsoft Press, 2004): the source for the simplicity and construction-practice framing behind the KISS and fail-fast sections above.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480/9780321700698) (Addison-Wesley, 1994): the source for composition over inheritance and the Strategy, Decorator, and Bridge examples above.
- Martin, [*Agile Software Development: Principles, Patterns, and Practices*](https://www.pearson.com/en-gb/subject-catalog/p/agile-software-development-principles-patterns-and-practices-pearson-new-international-edition/P200000004932/9781292038360) (Prentice Hall, 2002): the source for separation of concerns and the SOLID principles it builds toward.
- Brooks, [*The Mythical Man-Month: Essays on Software Engineering*](https://www.pearson.com/en-us/subject-catalog/p/Brooks-Mythical-Man-Month-Anniversary-Edition-The-Essays-On-Software-Engineering-2nd-Edition/P200000000149/9780132119160) (Addison-Wesley, 1995): the source for Brooks's Law.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
