# 26-expression-trees

Tariff rules authored as inspectable, rewritable data, using
`System.Linq.Expressions.Expression<TDelegate>` from the BCL, not a
hand-rolled Interpreter AST.

- **Authoring.** `TariffExpressionLibrary` exposes `WeightRate`, `ValueRate`,
  and `IsHeavy` as `Expression<Func<RateRequest, decimal>>` and
  `Expression<Func<RateRequest, bool>>` properties. Assigning a lambda to an
  `Expression<TDelegate>`-typed member does not produce a delegate; the
  compiler emits a tree of `Expression` nodes describing the lambda's
  parameter, body, and operators, and that tree is what the rest of this
  example works with.
- **Reading.** `RateDescriber` is an `ExpressionVisitor` that walks a rule and
  renders it as a short, human-readable string, and
  `ReferencedPropertyCollector` is a second `ExpressionVisitor` that walks a
  rule to report which `RateRequest` properties it touches. Neither compiles
  or runs anything; a rule built around a literal division by zero collects
  its referenced properties cleanly, because visiting a tree only reads it.
- **Rewriting.** `RateInflator` is a third `ExpressionVisitor` that overrides
  `VisitConstant` to scale every `decimal` constant in a rule by a factor,
  leaving every other constant untouched, and returns a brand-new
  `Expression<Func<RateRequest, decimal>>`. Expression trees are immutable, so
  a rewrite is always a new tree assembled from the pieces the visitor
  returns, never an in-place mutation of the original.
- **Composing.** `ParameterReplacer` rebinds every occurrence of one
  `ParameterExpression` in a tree onto another. `PredicateComposer.And` uses
  it to unify two independently authored predicates, each closed over its own
  parameter, onto a single shared parameter before joining their bodies with
  `Expression.AndAlso`.

**What the tests prove.** `DescribeAndInspectTests` compiles and runs an
authored rule, asserts `RateDescriber` renders both a single rule and a
composed binary tree as the expected string, and asserts
`ReferencedPropertyCollector` finds the properties a rule touches, including
on a rule that would throw `DivideByZeroException` if compiled and invoked,
proving the collector never executes it. `RewriteAndComposeTests` asserts
`RateInflator` scales a rule's decimal constants so the rewritten tree
compiles to the scaled result, and leaves a non-decimal constant inside a
mixed rule untouched; asserts `ParameterReplacer` rebinds a lambda body onto a
new parameter and that the rebuilt lambda still computes the same result; and
asserts `PredicateComposer.And` unifies two predicates onto one shared
parameter and enforces both conditions across every true/false combination.

Shipping domain, no containers, no runtime package: `System.Linq.Expressions`
ships in the BCL, so this project takes no `PackageReference` of its own.

## Build and test

```sh
cd examples/26-expression-trees
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
