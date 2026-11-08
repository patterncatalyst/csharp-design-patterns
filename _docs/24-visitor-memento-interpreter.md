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

- Model a small grammar as a closed `TariffRule` record hierarchy and compile it to a reusable `Func<Shipment, decimal>` with `System.Linq.Expressions`.
- Replace Visitor's `Accept`/`Visit` double dispatch with a recursive switch expression, and say why it still needs a discard arm.
- Capture and restore a booking with `BookingOriginator.Save()` and `Restore()`, and know when a shallow list copy is enough.
- Checkpoint and roll back through an `AmendmentCaretaker` that never reads a memento's fields.

## Why this matters

A tariff is a small grammar: a flat fee, a per-kilogram rate, a percentage surcharge over another rule, a sum of rules. Rating needs to read that grammar fast, sometimes in more than one way, and a booking amendment needs a way back to the booking as it stood before the edit. Interpreter, Visitor and Memento cover those three jobs. C# shrinks two of them: `System.Linq.Expressions` is a ready-made interpretation step, and a switch expression over records does Visitor's dispatch without a second class hierarchy.

{% include excalidraw.html name="24-visitor-memento-interpreter" caption="Figure 24.1: TariffRule's closed hierarchy of four cases gets read two ways: TariffEvaluator walks it with a recursive switch expression, pattern matching standing in for Visitor's double dispatch, while TariffCompiler compiles the same tree into a reusable delegate." %}

## Pattern: Interpreter

### The problem

A rating run prices thousands of shipments against the same tariff. The tariff is data, built from rules that nest, so it can't be a fixed method. Walking the rule tree for every shipment repeats the same structural work each time.

### The solution

**An Interpreter represents a grammar as a tree of objects and evaluates sentences in that grammar against a context.**

### How C# models it

The shipment is the context:

```csharp
public sealed record Shipment(decimal WeightKilograms, decimal DeclaredValue, string Destination);
```

The grammar is a closed abstract record with four cases:

```csharp
public abstract record TariffRule;

public sealed record FlatFee(decimal Amount) : TariffRule;

public sealed record PerKilogram(decimal Rate) : TariffRule;

public sealed record Surcharge(TariffRule Inner, decimal Percent) : TariffRule;

public sealed record Sum(IReadOnlyList<TariffRule> Rules) : TariffRule;
```

`FlatFee` and `PerKilogram` are terminal expressions. `Surcharge` and `Sum` are nonterminal: they combine the results of the rules they wrap. Callers build trees directly from the records, with no builder in the way.

### Compiling the tree to a delegate

`TariffCompiler` walks the rule once to build an expression tree, then compiles it:

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

The result is an ordinary `Func<Shipment, decimal>`. The tree walk happens once, at compile time, and every shipment after that runs straight-line arithmetic. Against a 10 kg shipment, `PerKilogram(4m)` returns 40, a 10% `Surcharge` over `FlatFee(100m)` returns 110, and a `Sum` of `FlatFee(10m)` and `PerKilogram(2m)` returns 30.

## Pattern: Visitor

### The problem

The same tariff tree needs a second reading: a direct evaluation for a one-off quote, where compiling isn't worth it. The operation differs by the rule's concrete type. Checking `GetType()` or a string tag in every branch is brittle, and adding an `Evaluate` method to each record mixes every operation into the grammar.

### The solution

**A Visitor moves an operation over a closed set of element types out of the elements, dispatching on both the operation and the element's concrete type.**

### How C# models it

A switch expression with type patterns does the dispatch that `Accept` and a `Visit` overload set exist to provide:

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

Each new operation is one more static method with one switch. There's no visitor interface and no `Accept` on the records.

### Why the discard arm stays

All four cases live in one file, but the compiler can't prove the hierarchy is closed: nothing stops another file or assembly from deriving a fifth `TariffRule`. Without the discard, the switch produces warning CS8509, which is an error under warnings-as-errors. The discard throws, so a fifth case added without updating `Evaluate` fails on first use instead of returning zero.

### Two readings, one answer

Both readings walk the same structure, so they agree. A `Sum` of a 20% `Surcharge` over `PerKilogram(3m)` and a `FlatFee(15m)`, against a 10 kg shipment, gives 51 from both `TariffEvaluator.Evaluate` and the compiled delegate: 30, scaled to 36, plus 15.

{% include excalidraw.html name="24-pattern-match-and-compiled-tree" caption="Figure 24.2: The same TariffRule tree evaluated two independent ways, a pattern-matching switch expression and a compiled delegate built from the identical tree, produces matching decimals against the same shipment." %}

| Reading | Cost before first result | Cost per shipment | Use it for |
| --- | --- | --- | --- |
| `TariffEvaluator.Evaluate` | none | full tree walk | one-off quotes, occasional rules |
| `TariffCompiler.Compile` | build and compile the tree | a delegate call | one tariff over a large rating run |

## Pattern: Memento

### The problem

A customer amends a booking: a different service level, an added line. If the amendment is rejected, the booking must return to exactly where it stood. The code that holds the undo history shouldn't need to know what a booking contains.

### The solution

**A Memento captures an object's internal state in an opaque snapshot so the object can be restored to it later, without exposing that state to the holder of the snapshot.**

### How C# models it

The snapshot is a record, and each line is an immutable record:

```csharp
public sealed record BookingLine(string Sku, int Quantity);
```

```csharp
public sealed record BookingMemento(
    ServiceLevel ServiceLevel,
    decimal WeightKilograms,
    IReadOnlyList<BookingLine> Lines);
```

The originator owns the mutable state and moves it into and out of a memento:

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

### Shallow copy, immutable elements

`[.. _lines]` builds a new list holding the same `BookingLine` references. That shallow copy isolates the snapshot only because `BookingLine` is immutable. A line added after `Save` goes into the originator's list, not the memento's, and `Restore` copies again so later edits never reach a stored snapshot. If the elements were mutable, the snapshot would need copies of them too.

### The caretaker

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

The caretaker pushes snapshots and hands them back unopened. The stack gives rollback its order: a multi-step amendment unwinds most recent first. Rolling back with no checkpoint throws `InvalidOperationException`.

{% include excalidraw.html name="24-memento-checkpoint-rollback" caption="Figure 24.3: BookingOriginator.Save captures its state into a BookingMemento, and AmendmentCaretaker pushes checkpoints onto its own stack without ever looking inside one. Rollback pops the most recent checkpoint and restores it, rebuilding state from a copy rather than an alias." %}

## When not to use it

- **Interpreter as a compiled tree.** Building and compiling the expression costs time before the first result. A rule evaluated once or rarely is cheaper through `TariffEvaluator.Evaluate`.
- **Switch-expression Visitor.** Every operation lists every case, so a fifth `TariffRule` means editing `Evaluate`, `Build` and any other switch. A hierarchy that changes often, or that other assemblies extend, is a poor fit; virtual methods on the records suit it better.
- **Memento on a plain stack.** Each checkpoint holds a full copy of the line list, and the stack only shrinks on rollback. A long-running or large booking needs a bounded history or an eviction policy.

<blockquote class="ai-sidebar" markdown="1">
**Specifying undo and tariff evaluation.** "Add a way to undo this" leaves open whether undo means replaying commands or restoring state, and how deep the copy goes. "Price the tariff" leaves open whether to compile. State both:

> Add `Save()` and `Restore()` to `BookingOriginator`, returning a `BookingMemento` record. Copy the line list with a collection expression; `BookingLine` is an immutable record, so don't clone the lines. Keep checkpoints in an `AmendmentCaretaker` stack that never reads memento fields. Evaluate a `TariffRule` with a recursive switch expression with a throwing discard arm, and compile it with `System.Linq.Expressions` only when one rule prices many shipments.

If the reply adds an `IUndoableAction` hierarchy or a recursive deep clone, ask what mutation it guards against.
</blockquote>

## Build, run, observe

```sh
cd examples/24-visitor-memento-interpreter
dotnet test
```

All 14 tests pass; `scripts/verify.sh` runs them with every other example. Two experiments:

- **Drop the discard arm** from `TariffEvaluator.Evaluate`. The build fails with CS8509 under warnings-as-errors.
- **Alias instead of copy.** Change `Save` to pass `_lines` itself. A line added after a checkpoint leaks into the snapshot, and the isolation test fails.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Visitor, Memento, and Interpreter.
- Microsoft Learn, [Switch expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression): exhaustiveness checking and warning CS8509.
- Microsoft Learn, [Pattern matching overview](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching): the type patterns both switch expressions use.
- Microsoft Learn, [Expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/): `System.Linq.Expressions` nodes and `Expression.Lambda<TDelegate>(...).Compile()`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
