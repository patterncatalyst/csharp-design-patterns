---
title: "Visitor, Memento, and Interpreter"
order: 24
layout: tutorial
sectionid: docs
part: "coordinating-traversing"
description: "Pattern matching as the Visitor alternative, and expression trees as the real Interpreter."
---

<!-- EXAMPLE (shipping domain): Tariff rules as an expression tree; booking amendment rollback via memento. -->

## Learning objectives

By the end of this chapter you can:

- Build a closed `TariffRule` hierarchy and compile an instance of it into a
  reusable `Func<Shipment, decimal>` through `TariffCompiler.Compile`, using
  `System.Linq.Expressions` nodes such as `Expression.Constant`,
  `Expression.Multiply`, `Expression.Property`, and `Expression.Add` before
  calling `Expression.Lambda<Func<Shipment,decimal>>(...).Compile()`.
- Explain why `TariffCompiler` is the GoF Interpreter pattern as originally
  described, with `TariffRule` standing in for the grammar and a compiled
  expression tree standing in for the interpretation step.
- Write a recursive switch expression, `TariffEvaluator.Evaluate`, that
  performs the same double dispatch a classic `Accept`/`Visit` pair exists to
  provide, and explain why a discard arm is required even though `TariffRule`
  has exactly four concrete cases.
- Capture and restore state with `BookingOriginator.Save()` and `Restore()`,
  and explain why copying the line list with `[.. _lines]` is a sufficient
  deep copy only because `BookingLine` is an immutable record.
- Use `AmendmentCaretaker`'s `Stack<BookingMemento>` to checkpoint and roll
  back a booking, and state why the caretaker never reads a memento's fields.

## Why this matters

Of the patterns this book covers, Interpreter has the thinnest real-world
coverage. Most systems never define their own expression grammar, so a
chapter on Interpreter risks staying abstract: a `Context` class, an
`AbstractExpression` with an `Interpret` method, a `TerminalExpression` and a
`NonterminalExpression`, none of it tied to a shape a reader has built before.
.NET gives the pattern a concrete home. `System.Linq.Expressions` is a real
grammar for runtime-constructed code, and `Expression.Lambda<TDelegate>(...)
.Compile()` is a real interpretation step that turns a tree of `Expression`
nodes into an executable delegate. A tariff rule for a shipment, a flat fee,
a per-kilogram rate, a percentage surcharge over another rule, or a sum of
several rules, is exactly the kind of small, composable grammar GoF had in
mind, and building it as an actual `Expression` tree rather than a textbook
diagram makes every claim in this chapter something a test can check.

Visitor exists to solve a narrower problem than its ceremony suggests: given
a closed set of element types, perform an operation that differs by concrete
type, without an `if`/`else if` chain checking `GetType()` or a string tag
in every element. The double dispatch a classic `Accept(IVisitor visitor)`
method and a matching `Visit(ConcreteElement element)` overload produce is a
mechanism for solving that problem, not the problem itself. A pattern-matching
switch expression over a closed record hierarchy, the same dispatch
`TariffEvaluator.Evaluate` runs, solves it directly: one recursive expression
reads as plainly as the data it walks, with no second class hierarchy of
visitors required to add a reading.

Memento is the most literal of the three. A booking amendment is a risky
edit: a customer asks for a different service level or an added line, and
the system needs a way back to the booking as it stood before that edit, in
case the amendment has to be undone. `BookingOriginator` captures its own
state into an opaque `BookingMemento`, and `AmendmentCaretaker` holds a
history of those snapshots without ever looking inside one. The three
patterns in this chapter sit together because they all answer a different
question about a `TariffRule` tree or a `BookingOriginator`: how is it
evaluated, how is it evaluated a different way, and how is state captured so
an evaluation's effects can be undone. None of them depends on a feature GoF
could not have described in 1994. What changed is which of the three now
reads as a small amount of code: `Expression.Lambda<Func<Shipment,
decimal>>(...).Compile()` is three lines doing exactly what a hand-rolled
tree-walking interpreter used to take a class hierarchy to express, and a
switch expression over four record cases is exactly as many lines as the
operation itself, not a class hierarchy sized for one.

{% include excalidraw.html name="24-visitor-memento-interpreter" caption="Figure 24.1: TariffRule is a closed record hierarchy with four cases, FlatFee, PerKilogram, Surcharge, and Sum. TariffEvaluator.Evaluate reads it with a recursive switch expression and returns a decimal directly, the pattern-matching alternative to a classic Visitor's double dispatch. TariffCompiler.Compile reads the same hierarchy into a System.Linq.Expressions tree and compiles it to a reusable Func<Shipment, decimal> delegate, the GoF Interpreter pattern as originally described." %}

## The real Interpreter: a closed grammar compiled to a delegate

The example behind this chapter lives in
`examples/24-visitor-memento-interpreter`. A shipment carries the facts a
tariff rule is evaluated against, the Interpreter pattern's context argument:

```csharp
public sealed record Shipment(decimal WeightKilograms, decimal DeclaredValue, string Destination);
```

`TariffRule` is the grammar. It is a closed abstract record, every concrete
case declared in the same file, with exactly four shapes:

```csharp
public abstract record TariffRule;

public sealed record FlatFee(decimal Amount) : TariffRule;

public sealed record PerKilogram(decimal Rate) : TariffRule;

public sealed record Surcharge(TariffRule Inner, decimal Percent) : TariffRule;

public sealed record Sum(IReadOnlyList<TariffRule> Rules) : TariffRule;
```

`FlatFee` and `PerKilogram` are terminal expressions: each carries the one
number it needs and nothing else to interpret. `Surcharge` and `Sum` are
nonterminal expressions: each wraps one or more `TariffRule` values and
combines their results rather than producing a number on its own. A
`Surcharge` over a `Sum` of a `FlatFee` and a `PerKilogram` is a small tree,
exactly the recursive grammar Interpreter describes, built entirely from
records a caller can construct directly with no builder or factory in the
way.

`TariffCompiler.Compile` is where that grammar becomes the GoF Interpreter
pattern rather than a description of it. It walks a `TariffRule` once, not
to compute a number, but to build a `System.Linq.Expressions` tree describing
how to compute one, and then compiles that tree to a delegate:

```csharp
public static class TariffCompiler
{
    public static Func<Shipment, decimal> Compile(TariffRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        ParameterExpression shipment = Expression.Parameter(typeof(Shipment), "shipment");
        return Expression.Lambda<Func<Shipment, decimal>>(Build(rule, shipment), shipment).Compile();
    }

    private static Expression Build(TariffRule rule, ParameterExpression shipment) => rule switch
    {
        FlatFee f => Expression.Constant(f.Amount),
        PerKilogram p => Expression.Multiply(
            Expression.Constant(p.Rate),
            Expression.Property(shipment, nameof(Shipment.WeightKilograms))),
        Surcharge s => Expression.Multiply(
            Build(s.Inner, shipment),
            Expression.Constant(1 + s.Percent / 100m)),
        Sum s => s.Rules.Aggregate(
            (Expression)Expression.Constant(0m),
            (accumulator, component) => Expression.Add(accumulator, Build(component, shipment))),
        _ => throw new ArgumentOutOfRangeException(nameof(rule)),
    };
}
```

`Expression.Parameter(typeof(Shipment), "shipment")` declares the one
parameter every compiled tariff rule accepts, a placeholder standing in for
whatever `Shipment` eventually gets passed to the compiled delegate. `Build`
turns each `TariffRule` case into the `Expression` node that reproduces its
meaning as runtime code rather than as a recursive call: a `FlatFee` becomes
an `Expression.Constant` holding its amount directly, and a `PerKilogram`
becomes an `Expression.Multiply` of a constant rate against
`Expression.Property(shipment, nameof(Shipment.WeightKilograms))`, a property
read expressed as data rather than as a direct `shipment.WeightKilograms`
access. `Surcharge` multiplies whatever expression its inner rule built by a
constant derived from its percent, and `Sum` folds its component rules with
`Aggregate`, starting from `Expression.Constant(0m)` and wrapping each
component's expression in `Expression.Add` as it goes, so a `Sum` of three
rules becomes two nested `Expression.Add` nodes rather than three separate
additions computed independently. `Compile` hands the finished tree, together
with the `shipment` parameter, to
`Expression.Lambda<Func<Shipment, decimal>>(...).Compile()`, which turns the
whole structure into an ordinary `Func<Shipment, decimal>` a caller invokes
like any other delegate, with the tree-walking cost paid once at compile time
rather than once per shipment evaluated.

`TariffInterpreterTests` checks each case against a fixed `Shipment` with a
ten-kilogram weight. `Compiled_flat_fee_returns_the_fee` compiles a
`FlatFee(25m)` and asserts the delegate returns `25m` regardless of the
shipment passed in. `Compiled_per_kilogram_scales_with_weight` compiles a
`PerKilogram(4m)` and asserts `40m` against the ten-kilogram shipment, the
rate multiplied through the compiled `Expression.Property` read.
`Compiled_surcharge_applies_percent_over_inner` compiles a `Surcharge` of a
`FlatFee(100m)` at ten percent and asserts `110m`, and
`Compiled_sum_adds_component_rules` compiles a `Sum` of a `FlatFee(10m)` and
a `PerKilogram(2m)` and asserts `30m`, the two component results added
through the `Aggregate` fold rather than computed by two separate delegate
calls. Each test compiles its rule once and invokes the result once, but the
same compiled delegate is reusable across any number of shipments, which is
the entire point of paying the tree-building cost up front: a tariff rule
checked against a thousand shipments in a rating run builds its expression
tree exactly once.

## Visitor's double dispatch, met by pattern matching

A second way to read a `TariffRule` tree needs no expression tree at all.
`TariffEvaluator.Evaluate` walks the same four cases with a recursive switch
expression and returns a `decimal` directly, computing the answer rather than
building a description of how to compute it:

```csharp
public static class TariffEvaluator
{
    public static decimal Evaluate(TariffRule rule, Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(shipment);

        return rule switch
        {
            FlatFee f => f.Amount,
            PerKilogram p => p.Rate * shipment.WeightKilograms,
            Surcharge s => Evaluate(s.Inner, shipment) * (1 + s.Percent / 100m),
            Sum s => s.Rules.Sum(r => Evaluate(r, shipment)),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
    }
}
```

This is the double dispatch a classic Visitor exists to provide, performed
without a second class hierarchy. A Visitor's `Accept`/`Visit` pair lets an
operation vary by an element's concrete type without the element itself
knowing what the operation does; `Evaluate`'s switch expression gets the same
result by matching each pattern, `FlatFee f`, `PerKilogram p`, `Surcharge s`,
`Sum s`, against the rule's concrete runtime type directly, with the match
itself doing the dispatch a `Visit` overload set would otherwise resolve
through method overload selection. `Surcharge` and `Sum` recurse into
`Evaluate` the same way `Build` recurses into itself in the compiler, which
is why both readings of the tree agree: they walk the identical structure,
one producing a number immediately, the other producing an `Expression` that
will produce a number later.

The discard arm, `_ => throw new ArgumentOutOfRangeException(nameof(rule))`,
is required even though every concrete case `TariffRule` can take lives in
one file. The compiler has no way to prove that a record hierarchy is closed;
nothing in the language stops a `TariffRule` subclass from being declared in
a different file or a different assembly entirely, so a switch expression
over `FlatFee`, `PerKilogram`, `Surcharge`, and `Sum` alone produces warning
CS8509 for a pattern match that might not be exhaustive. The discard arm
resolves that warning by giving every case the compiler cannot rule out a
defined behavior, throwing rather than silently returning a default decimal,
so a fifth case added later without updating `Evaluate` fails loudly the
first time it runs instead of quietly returning zero.

`TariffEvaluatorTests.Pattern_match_evaluates_a_nested_tree` builds a
`Surcharge` of a `Sum` of a `FlatFee(50m)` and a `PerKilogram(5m)`, at ten
percent, and asserts `110m` against a ten-kilogram shipment: the `Sum`
resolves to `50m + 5m * 10m`, or `100m`, and the surrounding `Surcharge`
scales that by ten percent to `110m`.
`Pattern_match_handles_each_rule_kind` is a `Theory` covering all four cases
against a five-kilogram shipment, asserting `20m` for a bare `FlatFee(20m)`,
`15m` for a `PerKilogram(3m)`, `15m` for a `Surcharge(FlatFee(10m), 50m)`,
and `15m` for a `Sum` of a `FlatFee(5m)` and a `PerKilogram(2m)`, one case
per row rather than one assertion per test method.

A single tree read both ways is where the two patterns meet. The test
`Compiled_delegate_matches_the_pattern_matching_evaluator` builds a `Sum` of
a `Surcharge` over a `PerKilogram(3m)` at twenty percent and a flat
`FlatFee(15m)`, evaluates it with `TariffEvaluator.Evaluate` and separately
with the delegate `TariffCompiler.Compile` produces, and asserts the two
decimals are equal. Against the ten-kilogram shipment this tree resolves to,
the `PerKilogram(3m)` reads as `30m`, the surrounding `Surcharge` at twenty
percent scales it to `36m`, and the outer `Sum` adds the `FlatFee(15m)` to
land on `51m`, the same `51m` whether the tree is walked directly by the
switch expression or invoked as a compiled `Func<Shipment, decimal>` built
from `Expression` nodes describing the identical arithmetic. Two independent
readings of one data structure landing on the same number is exactly the
guarantee an operation added to a closed hierarchy needs to keep: whichever
reading a caller reaches for, pattern-matching evaluation for a one-off
calculation or a compiled delegate for a tariff rule applied across a rating
run, the answer does not depend on which one was chosen.

{% include excalidraw.html name="24-pattern-match-and-compiled-tree" caption="Figure 24.2: The same TariffRule tree, a Sum of a Surcharge over a PerKilogram and a FlatFee, read two ways against the same Shipment. TariffEvaluator.Evaluate walks it with a recursive switch expression, and TariffCompiler.Compile(tree)(shipment) invokes the compiled delegate built from the same tree. Compiled_delegate_matches_the_pattern_matching_evaluator asserts the two decimals are equal." %}

## Memento: snapshot, checkpoint, and rollback

A booking amendment is a different kind of problem from reading a tariff
tree: a `BookingOriginator` holds mutable state that a caller wants to edit
provisionally, with a way back to where it stood if the edit turns out to be
wrong. `BookingLine` is the smallest unit of that state, an immutable record
carrying one line item:

```csharp
public sealed record BookingLine(string Sku, int Quantity);
```

`BookingMemento` is the snapshot, a record capturing every field
`BookingOriginator` needs restored, opaque to anything that is not the
originator that produced it:

```csharp
public sealed record BookingMemento(
    ServiceLevel ServiceLevel,
    decimal WeightKilograms,
    IReadOnlyList<BookingLine> Lines);
```

`BookingOriginator` owns the mutable state and the two operations that move
it into and out of a `BookingMemento`:

```csharp
public sealed class BookingOriginator
{
    private List<BookingLine> _lines = [];

    public ServiceLevel ServiceLevel { get; private set; }

    public decimal WeightKilograms { get; private set; }

    public IReadOnlyList<BookingLine> Lines => _lines;

    public void SetServiceLevel(ServiceLevel serviceLevel) => ServiceLevel = serviceLevel;

    public void SetWeight(decimal weightKilograms) => WeightKilograms = weightKilograms;

    public void AddLine(BookingLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
    }

    public BookingMemento Save() => new(ServiceLevel, WeightKilograms, [.. _lines]);

    public void Restore(BookingMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        ServiceLevel = memento.ServiceLevel;
        WeightKilograms = memento.WeightKilograms;
        _lines = [.. memento.Lines];
    }
}
```

`Save` and `Restore` both use the collection expression `[.. _lines]` and
`[.. memento.Lines]` to build a new `List<BookingLine>` rather than handing
out or taking in a reference to the existing one. This is the deep-versus-
shallow copy lesson the example is built to make concrete: copying the list
itself is a shallow operation, a new list holding the same `BookingLine`
references the old one held, and that shallow copy is sufficient here only
because `BookingLine` is an immutable record. Nothing in this codebase can
mutate a `BookingLine` once constructed, so a `BookingMemento` holding
references to the same line instances a `BookingOriginator` is still using
is exactly as isolated as one holding freshly cloned lines would be; the only
way `Lines` can change after a snapshot is taken is by `AddLine` appending to
`_lines`, which replaces nothing already in the memento's copy, and `Restore`
replacing `_lines` wholesale with a fresh copy built from the memento,
leaving the memento's own list untouched by whatever mutation happens to the
originator next.

`AmendmentCaretaker` holds the history of checkpoints and never reads what is
inside one:

```csharp
public sealed class AmendmentCaretaker
{
    private readonly Stack<BookingMemento> _history = new();

    public void Checkpoint(BookingOriginator originator)
    {
        ArgumentNullException.ThrowIfNull(originator);
        _history.Push(originator.Save());
    }

    public void Rollback(BookingOriginator originator)
    {
        ArgumentNullException.ThrowIfNull(originator);

        if (_history.Count == 0)
        {
            throw new InvalidOperationException("No checkpoint has been recorded to roll back to.");
        }

        originator.Restore(_history.Pop());
    }
}
```

`Checkpoint` calls `originator.Save()` and pushes the result; `Rollback` pops
the most recent one and hands it straight to `originator.Restore(...)`. At no
point does `AmendmentCaretaker` read `ServiceLevel`, `WeightKilograms`, or
`Lines` off a `BookingMemento` directly; a `Stack<BookingMemento>` is the
entire data structure the caretaker needs, because the only two operations it
performs on a checkpoint are pushing it and handing it back unopened. The
`Stack<BookingMemento>` shape also gives rollback its ordering for free: the
most recently pushed checkpoint is the first one popped, so a sequence of
checkpoints taken during a multi-step amendment rolls back one step at a
time, most recent first, without the caretaker tracking order itself.

`MementoTests` checks three distinct guarantees.
`Restore_rolls_back_service_level_and_weight` sets a service level and
weight, saves a memento directly, changes both fields again, and asserts
`Restore` brings both back to their saved values.
`Rollback_through_caretaker_restores_the_last_checkpoint` routes the same
round trip through `AmendmentCaretaker.Checkpoint` and `Rollback` rather than
calling `Save`/`Restore` directly, confirming the caretaker's stack-based
indirection produces the same result as the originator's own methods.
`Memento_snapshot_is_isolated_from_later_mutation` is the deep-copy guarantee
itself: it adds one `BookingLine`, saves a snapshot and checks a checkpoint,
adds a second line, rolls back, and asserts the originator's `Lines` holds
only the first line again while the standalone `snapshot` variable, taken
before the second line was ever added, still holds exactly one line. A
caretaker or an originator that aliased the list instead of copying it would
fail this test by letting the second `AddLine` call leak into a snapshot
taken before it ran. `Rollback_with_no_checkpoint_throws` confirms calling
`Rollback` against an empty `Stack<BookingMemento>` raises an
`InvalidOperationException` rather than returning silently or indexing an
empty stack.

{% include excalidraw.html name="24-memento-checkpoint-rollback" caption="Figure 24.3: BookingOriginator.Save() captures ServiceLevel, WeightKilograms, and a copy of Lines into a new BookingMemento. AmendmentCaretaker.Checkpoint pushes that memento onto its own Stack<BookingMemento> without ever looking inside it, and Rollback pops the most recent one and hands it to BookingOriginator.Restore(memento), which rebuilds _lines from the memento's copy rather than aliasing it." %}

## Build, run, observe

Every type in this example, `TariffRule` and its four cases, `TariffEvaluator`,
`TariffCompiler`, and the three Memento types, is synchronous and in-memory,
so the suite needs nothing beyond the local .NET 10 SDK. The example is
standalone-testable from its own directory:

```sh
cd examples/24-visitor-memento-interpreter
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 14
  failed: 0
  succeeded: 14
  skipped: 0
  duration: 801ms
```

The fourteen cases split across what this chapter covers. `TariffEvaluatorTests`
proves the pattern-matching half: a nested `Surcharge`-over-`Sum` tree
evaluates to the right total, and a `Theory` sweeps all four `TariffRule`
cases against their expected decimals in one table. `TariffInterpreterTests`
proves the compiled half: each of the four cases compiles to a delegate that
returns the right decimal when invoked, and one further case,
`Compiled_delegate_matches_the_pattern_matching_evaluator`, builds a single
tree and asserts the compiled delegate and the pattern-matching evaluator
agree on it exactly. `MementoTests` proves the snapshot-and-rollback half:
a direct `Save`/`Restore` round trip, the same round trip routed through
`AmendmentCaretaker`, a snapshot's isolation from a line added after it was
taken, and an `InvalidOperationException` when `Rollback` runs against an
empty history.

## When not to use it

An expression tree earns its cost when the same `TariffRule` is compiled
once and the resulting delegate is invoked many times, a rating engine
checking the same tariff structure against thousands of shipments in a
batch. Building `ParameterExpression`, walking every node through `Build`,
and calling `Expression.Lambda<TDelegate>(...).Compile()` all cost real time
before the first invocation returns a result, time a plain method call or a
single pattern-matching `Evaluate` pays nothing for. A tariff rule checked
against exactly one shipment, or evaluated only occasionally, gets nothing
from compiling an expression tree that a direct call to `TariffEvaluator
.Evaluate` would not say in less time and fewer allocations.

A recursive switch expression over a closed hierarchy is only as cheap to
maintain as the hierarchy stays closed. `TariffEvaluator.Evaluate` and
`TariffCompiler.Build` each enumerate all four `TariffRule` cases
independently, and a fifth case added to the grammar means updating both
switch expressions, plus any other operation written the same way, every
time. A hierarchy that changes shape often, or that other assemblies are
expected to extend with their own cases, costs more to maintain this way
than one where every case is fixed and every operation over it lives in the
same codebase; a hierarchy under active, frequent change in exactly this
chapter's example would be a sign to reconsider the shape rather than keep
adding discard-protected switch expressions.

A `Stack<BookingMemento>` held by an `AmendmentCaretaker` grows by one entry
per `Checkpoint` call and shrinks only when `Rollback` pops one. A booking
amendment workflow that checkpoints frequently, holds a long history of
checkpoints without ever rolling any of them back, or snapshots a booking
with a very large number of lines, accumulates memory proportional to how
many checkpoints are still on the stack, each one a full copy of the line
list at that point. A caretaker is the right shape for a bounded amendment
flow with a short, deliberately managed history; an unbounded one needs an
eviction policy the plain `Stack<BookingMemento>` in this example does not
provide.

> **AI sidebar.** Asked to add "a way to undo this," an AI pairing partner
> will sometimes reach for a full command-based undo stack, with an
> `IUndoableAction` interface and a history of action objects, for a problem
> that is really just capturing and restoring an originator's own state, the
> shape `BookingOriginator.Save()` and `Restore()` already cover in two
> methods. A related suggestion defensively deep-clones every field a
> memento captures, writing a recursive copy routine for `BookingLine` even
> though `BookingLine` is an immutable record and a shallow list copy is
> already sufficient, adding cloning logic that protects against a mutation
> the type system already rules out. On the Interpreter side, a generated
> answer can reach for `System.Linq.Expressions` and a compiled delegate for
> a tariff rule that will only ever be evaluated once, paying the cost of
> building and compiling a tree to save nothing over a direct
> `TariffEvaluator.Evaluate` call. Check a generated answer against what
> this chapter actually built: compile a rule when it will run many times
> against many shipments, evaluate it directly with pattern matching when it
> will not, and let a memento's own record immutability do the isolation
> work before reaching for a hand-written deep clone.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Visitor, Memento, and Interpreter this chapter builds on.
- Microsoft Learn, [Switch expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression): the authority for switch-expression exhaustiveness checking and the CS8509 warning behind this chapter's discard arm in `TariffEvaluator.Evaluate` and `TariffCompiler.Build`.
- Microsoft Learn, [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching): the authority for the type-pattern matching, `FlatFee f`, `PerKilogram p`, and so on, both switch expressions in this chapter use to dispatch on a `TariffRule`'s concrete type.
- Microsoft Learn, [Expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/): the authority for `System.Linq.Expressions`, the `Expression.Constant`, `Expression.Multiply`, `Expression.Property`, and `Expression.Add` nodes `TariffCompiler.Build` constructs, and the `Expression.Lambda<TDelegate>(...).Compile()` step that turns a tree into a delegate.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
