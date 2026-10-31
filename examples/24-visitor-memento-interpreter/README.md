# 24-visitor-memento-interpreter

Three patterns over the same shipment tariff domain: two ways to add a new
operation to a closed data structure without touching the data types
themselves, and one way to undo a change to a booking.

- **Interpreter.** `TariffRule` is a closed expression tree: `FlatFee`,
  `PerKilogram`, `Surcharge`, and `Sum`. `TariffCompiler.Compile` walks the
  tree once with `System.Linq.Expressions`, building a `ParameterExpression`
  for the `Shipment` and composing `Expression.Constant`,
  `Expression.Multiply`, and `Expression.Add` nodes that mirror the tree's
  shape, then compiles the result to a `Func<Shipment, decimal>`. Compiling
  once and invoking the delegate many times amortizes the walk; a tree-walking
  evaluator repeats it on every call.
- **Visitor (pattern-matching form).** `TariffEvaluator.Evaluate` is a
  recursive `switch` expression over the same closed `TariffRule` hierarchy.
  It performs the double dispatch a classic `Visit`-method Visitor exists to
  provide, using the language's own exhaustive pattern matching instead of a
  parallel visitor interface. A discard arm throws
  `ArgumentOutOfRangeException`, since the compiler cannot prove the switch is
  exhaustive over a record hierarchy on its own.
- **Memento.** `BookingOriginator` holds a booking's `ServiceLevel`,
  `WeightKilograms`, and `Lines`. `Save` captures a `BookingMemento` snapshot
  with its own copy of the line list; `Restore` rebuilds the originator's
  internal list from the memento's copy rather than aliasing it, so a later
  mutation of the booking can never reach back into a snapshot.
  `AmendmentCaretaker` keeps a stack of mementos and never inspects what is
  inside one: `Checkpoint` pushes a snapshot, `Rollback` pops and restores the
  most recent one, and throws `InvalidOperationException` if the stack is
  empty.

**What the tests prove.** `TariffInterpreterTests` checks `TariffCompiler`
against each rule kind individually, then against a nested
`Sum`/`Surcharge`/`PerKilogram`/`FlatFee` tree where the compiled delegate's
result is asserted equal to `TariffEvaluator.Evaluate` on the same tree, the
core proof that the two interpretation strategies agree.
`TariffEvaluatorTests` evaluates a nested tree directly and runs a
`[Theory]` over all four rule kinds. `MementoTests` checks that `Restore`
rolls back both scalar fields, that `AmendmentCaretaker.Rollback` restores the
last checkpoint, that a snapshot taken before a later `AddLine` is unaffected
by it, and that rolling back with no checkpoint throws.

Shipping domain, no containers: all three patterns are pure, synchronous,
in-memory collaborations, no network or I/O to fake out.

## Build and test

```sh
cd examples/24-visitor-memento-interpreter
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
