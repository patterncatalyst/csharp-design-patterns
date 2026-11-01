---
title: "Expression trees: code as data"
order: 26
layout: tutorial
sectionid: docs
part: "idiomatic-csharp"
description: "Code as inspectable data at runtime: how LINQ providers work, and how to build and inspect trees."
---

<!-- EXAMPLE (shipping domain): The tariff rule engine as inspectable expression trees. -->

## Learning objectives

By the end of this chapter you can:

- Author a tariff rule as an `Expression<Func<RateRequest, decimal>>`, the
  shape `TariffExpressionLibrary.WeightRate` takes, and explain what the
  compiler builds in place of a lambda's usual executable code when the
  target type is `Expression<TDelegate>` rather than `Func<TDelegate>`.
- Write an `ExpressionVisitor` override, `RateDescriber`, that renders a
  rule's tree as a readable string through `VisitBinary`, `VisitMember`,
  `VisitConstant`, and `VisitParameter`, and explain why `Describe` never
  compiles or runs the rule it reads.
- Use `ReferencedPropertyCollector.Collect` to find which `RateRequest`
  properties a rule touches by visiting the tree alone, and explain why a
  rule that would throw on execution, a division by a literal zero, collects
  cleanly anyway.
- Rewrite a tree with `RateInflator.Inflate`, scaling every `decimal`
  constant by a factor while leaving every other constant untouched, and
  explain why `VisitConstant` hands back a brand-new node rather than
  mutating the one it receives.
- Splice two independently authored predicates into one shared-parameter
  lambda with `ParameterReplacer` and `PredicateComposer.And`, then call
  `Expression<T>.Compile()` to turn the composed tree into a runnable
  delegate.

## Why this matters

A lambda ordinarily compiles straight to executable code: `r =>
r.WeightKilograms * 2.50m` becomes a method body, and calling it runs that
body. .NET gives the same syntax a second meaning. Write the same lambda
against a target type of `Expression<Func<RateRequest, decimal>>` instead of
`Func<RateRequest, decimal>`, and the compiler builds a tree of `Expression`
objects describing the multiplication, the property read, and the constant,
rather than a method body that performs them. Nothing about the lambda's
text changes. What changes is what the compiler produces from it, data that
represents a computation rather than code that runs one.

That distinction is the reason `System.Linq.Expressions` exists at all, and
it is also how a LINQ provider translates a query instead of running it.
When a LINQ call against a non-enumerable source accepts an
`Expression<Func<TSource, TResult>>` parameter rather than a `Func<TSource,
TResult>`, the provider behind that call receives the tree the lambda
compiled to, not a delegate it could invoke directly. A provider backed by a
database reads that tree the way `RateDescriber` reads a tariff rule in this
chapter, walking its nodes to find a property read here, a comparison there,
and rebuilding the equivalent operation in its own target language before
any row comes back. The tree is the only reason a provider can do that:
a `Func<RateRequest, decimal>` is a black box the runtime can call but never
read, while an `Expression<Func<RateRequest, decimal>>` is a data structure
the runtime, and any code holding a reference to it, can inspect, describe,
rewrite, or compile, in whichever order the situation calls for.

This chapter builds a small rate-rule library around that same shape. A rule
starts as an `Expression<Func<RateRequest, decimal>>` or an
`Expression<Func<RateRequest, bool>>`, exactly the kind of tree a query
provider would receive, and the rest of the example does to it what a
provider, a validator, or a rule-editing tool would do in turn: read it
without running it, scale its constants into a new tree, and splice two
independently written predicates into one before finally compiling anything
to a delegate. Every operation in the example stops short of execution until
the last possible step, because that is the entire value an expression tree
offers over a plain delegate: the chance to look at a computation, and
change it, before it ever runs.

{% include excalidraw.html name="26-expression-trees" caption="Figure 26.1: A tariff rate rule compiles to a tree of Expression nodes rather than executable code. RateDescriber reads that tree and renders it as a string without running it, while Compile takes the same tree down a different path into a runnable delegate." %}

## Authoring a rule as data

The example behind this chapter lives in `examples/26-expression-trees`. A
rate request carries the facts a tariff rule is evaluated against:

```csharp
public sealed record RateRequest(decimal WeightKilograms, decimal DeclaredValue, string Destination, bool Hazardous);
```

`TariffExpressionLibrary` holds three rules, each one an `Expression`
property rather than a method:

```csharp
public static class TariffExpressionLibrary
{
    public static Expression<Func<RateRequest, decimal>> WeightRate => r => r.WeightKilograms * 2.50m;

    public static Expression<Func<RateRequest, decimal>> ValueRate => r => r.DeclaredValue * 0.015m;

    public static Expression<Func<RateRequest, bool>> IsHeavy => r => r.WeightKilograms > 20m;
}
```

The property type is the entire story. `WeightRate`'s declared type is
`Expression<Func<RateRequest, decimal>>`, so the compiler reads the lambda
`r => r.WeightKilograms * 2.50m` and, instead of emitting a method, builds an
object graph: a `ParameterExpression` named `r`, a `MemberExpression` reading
`WeightKilograms` off it, a `ConstantExpression` holding `2.50m`, and a
`BinaryExpression` combining the two with `ExpressionType.Multiply`, all
wrapped in a `LambdaExpression` that records the parameter list and the body.
That graph is what `TariffExpressionLibrary.WeightRate` returns on every
access, a tree a caller can walk field by field. `IsHeavy` goes through the
same conversion with a `GreaterThan` node in place of `Multiply`, because the
lambda-to-tree conversion applies to boolean-returning lambdas exactly as it
does to decimal-returning ones; the target type, not the body, decides
whether a lambda becomes a tree or a delegate.

`DescribeAndInspectTests.Authored_rule_compiles_and_computes_the_rate`
confirms the tree is still a faithful description of the arithmetic by
calling `TariffExpressionLibrary.WeightRate.Compile()` and invoking the
result against a ten-kilogram request, asserting `25.00m`. Compiling and
invoking a rule is the ordinary path an expression tree supports, and
nothing in this chapter changes that path. What `Expression<TDelegate>`
adds is everything a caller can do to the tree before reaching for
`Compile()`: describe it, inspect it, rewrite it, or splice it onto another
tree, each one covered next with its own type from the example.

## Reading a tree without running it

`RateDescriber` is an `ExpressionVisitor`, the base class
`System.Linq.Expressions` ships for walking a tree node by node. Its
`Describe` method visits a rule's body and renders it as a short, readable
string:

```csharp
public sealed class RateDescriber : ExpressionVisitor
{
    private readonly StringBuilder _text = new();

    public string Describe(Expression<Func<RateRequest, decimal>> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _text.Clear();
        Visit(rule.Body);
        return _text.ToString();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _text.Append('(');
        Visit(node.Left);
        _text.Append(' ').Append(DescribeOperator(node.NodeType)).Append(' ');
        Visit(node.Right);
        _text.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression parameter)
        {
            _text.Append(parameter.Name).Append('.').Append(node.Member.Name);
        }
        else
        {
            Visit(node.Expression);
            _text.Append('.').Append(node.Member.Name);
        }

        return node;
    }
}
```

`ExpressionVisitor` dispatches to the right `VisitXxx` override for each
node kind on its own; calling `Visit(rule.Body)` on a `BinaryExpression`
lands in `VisitBinary`, and `Visit`-ing that node's `Left` and `Right`
children sends each of them back through the same dispatch, into
`VisitMember` for a property read or `VisitConstant` for a literal. This is
the base class doing a traversal a hand-written tree walker would otherwise
need its own switch for, one override per node shape the visitor cares
about, with every other node kind falling through to a default that recurses
into children without otherwise touching them. `RateDescriber` overrides
four: `VisitBinary` wraps an operator in parentheses between its operands,
`VisitMember` renders a parameter-qualified property read as `r.
WeightKilograms`, `VisitConstant` appends a literal's value directly, and
`VisitParameter` appends a bare parameter name for the rare case a tree
returns its parameter unchanged.

`DescribeAndInspectTests.Describe_renders_a_readable_string_for_a_weight_rule`
calls `Describe(TariffExpressionLibrary.WeightRate)` and asserts the result
is `"(r.WeightKilograms * 2.50)"`, the multiplication's two operands
rendered on either side of the operator symbol `VisitBinary` looks up from
`node.NodeType`. `Describe_handles_a_composed_binary_tree` builds a two-term
sum inline, `r => (r.WeightKilograms * 2.50m) + (r.DeclaredValue *
0.015m)`, and asserts the describer renders nested parentheses correctly,
`"((r.WeightKilograms * 2.50) + (r.DeclaredValue * 0.015))"`, because
`VisitBinary` recurses into its own `Left` and `Right` through `Visit`
exactly as it would into any other node kind, so a `BinaryExpression` whose
operands are themselves `BinaryExpression` nodes renders correctly without
`RateDescriber` needing to know how deep a tree it was handed. At no point
during either test does `Describe` call `Compile()` or invoke anything
these trees describe; the rule's value as a decimal is never computed, only
its shape as data.

## Reading before translating: the LINQ-provider angle

`ReferencedPropertyCollector` reads a different fact off the same kind of
tree, which properties a rule depends on, again without running it:

```csharp
public sealed class ReferencedPropertyCollector : ExpressionVisitor
{
    private readonly HashSet<string> _properties = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Collect(Expression rule)
    {
        _properties.Clear();
        Visit(rule);
        return _properties.ToArray();
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression && node.Member.DeclaringType == typeof(RateRequest))
        {
            _properties.Add(node.Member.Name);
        }

        return base.VisitMember(node);
    }
}
```

`VisitMember` checks whether the member access reads directly off the
lambda's own parameter and belongs to `RateRequest`, and if so records the
property name, then calls `base.VisitMember(node)` so the default traversal
still visits the node's children, in case a property read is nested inside
something more complex than a top-level member access. `DescribeAndInspect
Tests.ReferencedPropertyCollector_finds_the_properties_a_rule_touches`
collects the two-term sum from the describer test and asserts the result
contains exactly `WeightKilograms` and `DeclaredValue`, the two properties
the rule's arithmetic reads.

The sharper test is
`ReferencedPropertyCollector_reads_without_executing`. It builds `r =>
r.WeightKilograms / 0m`, a rule that throws `DivideByZeroException` the
moment it runs, and asserts two things in the same test: `Collect` on that
tree returns `WeightKilograms` without raising anything, and compiling the
same tree and invoking it against a request does throw. Collecting the
properties a rule touches is reading data, not running a computation, so a
tree that cannot safely execute is exactly as safe to inspect as any other
tree. This is the behavior a LINQ provider depends on every time it looks at
a `Where` or `Select` argument before deciding how to answer a query: a
provider reading `Expression<Func<TSource, bool>>` to find out which column
a filter compares against, or which properties a projection selects, never
needs to run the predicate against an in-memory `TSource` first. It reads
the tree, decides what the equivalent operation looks like in its own
target, and only then produces a result, a sequencing `ReferencedProperty
Collector` mirrors directly: inspect the data describing a computation, and
let a decision about whether to compile and run it come after.

{% include excalidraw.html name="26-inspecting-a-tree" caption="Figure 26.3: ReferencedPropertyCollector walks a rule's tree and records which properties it touches without ever executing it, the way a LINQ provider reads a query before translating it. Compile takes the same tree down the other path into a runnable delegate." %}

## Rewriting a tree without mutating it

`RateInflator` goes one step further than reading: it builds a new tree out
of an old one, scaling every `decimal` constant by a factor while leaving
every other kind of node untouched:

```csharp
public sealed class RateInflator : ExpressionVisitor
{
    private decimal _factor;

    public Expression<Func<RateRequest, decimal>> Inflate(Expression<Func<RateRequest, decimal>> rule, decimal factor)
    {
        _factor = factor;
        return (Expression<Func<RateRequest, decimal>>)Visit(rule)!;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        return node.Value is decimal amount
            ? Expression.Constant(amount * _factor, typeof(decimal))
            : base.VisitConstant(node);
    }
}
```

`Expression` nodes are immutable, so there is no in-place way to change a
`ConstantExpression`'s value even if `RateInflator` wanted one. `VisitConstant`
instead returns an entirely new `Expression.Constant` holding the scaled
amount whenever the original constant was a `decimal`, and defers to
`base.VisitConstant(node)`, which returns the original node unchanged, for
anything else. `ExpressionVisitor`'s default traversal then rebuilds every
ancestor of a changed node with the new child in place of the old one, so
scaling one `ConstantExpression` ripples up through the `BinaryExpression`
that held it, and the `LambdaExpression` that held that, until `Inflate`
gets back a complete tree that shares every unscaled node with the original
and replaces only the ones `VisitConstant` touched.

`RewriteAndComposeTests.Inflate_scales_decimal_constants_and_the_rewritten
_tree_compiles_to_the_scaled_result` inflates `WeightRate` by a factor of
`1.10m`, compiles the result, and asserts a ten-kilogram request now rates
at `27.5m` rather than `25.00m`, the `2.50m` constant scaled before
compilation ever runs. `Inflate_leaves_non_decimal_constants_untouched`
checks the other half of the contract with a rule that branches on a string
comparison, `r.Destination == "Remote" ? r.WeightKilograms * 2.50m :
r.WeightKilograms * 2.00m`: inflating by a factor of `2m` and compiling
scales both `decimal` rates correctly, `50m` for a remote destination and
`40m` otherwise, while the string literal `"Remote"` the conditional
compares against is a `ConstantExpression` too, one `VisitConstant` leaves
alone because its `Value` is not a `decimal`. A rewrite that touched every
constant indiscriminately would corrupt that comparison; checking the
runtime type of `node.Value` before deciding what to do is what keeps the
rewrite scoped to the rates the rule charges.

{% include excalidraw.html name="26-expressionvisitor-rewrite" caption="Figure 26.2: RateInflator rewrites a tree by scaling its decimal constants, returning a brand-new tree because expression trees are immutable. PredicateComposer rebinds one predicate's parameter onto another's and joins both bodies into a single lambda over one shared parameter." %}

## Building trees with the factory API

Every tree so far came from a lambda the compiler converted automatically.
`ParameterReplacer` and `PredicateComposer` build a tree by hand instead,
using the `Expression` factory methods directly, because the problem they
solve has no lambda syntax that expresses it: joining two rules that were
each written against their own parameter into one rule over a single shared
parameter.

`TariffExpressionLibrary.IsHeavy` and a hazardous-flag predicate written
inline, `r => r.Hazardous`, are each a complete
`Expression<Func<RateRequest, bool>>` with its own `ParameterExpression`
named `r`. Two parameters sharing a name are still two distinct objects, so
pulling both bodies out directly and combining them with
`Expression.AndAlso` would build a tree where half the nodes reference one
parameter and half reference the other, a lambda with no single parameter
list that could ever be compiled. `ParameterReplacer` exists to fix that
before the join happens:

```csharp
public sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _from;
    private readonly ParameterExpression _to;

    public ParameterReplacer(ParameterExpression from, ParameterExpression to)
    {
        _from = from;
        _to = to;
    }

    protected override Expression VisitParameter(ParameterExpression node) =>
        node == _from ? _to : base.VisitParameter(node);
}
```

Every occurrence of `_from` inside a visited tree comes back as `_to`
instead, and every other node passes through `base.VisitParameter`
unchanged, which for a `ParameterExpression` that is not `_from` returns
the node itself with no substitution. `PredicateComposer.And` uses that rebinding to
unify two predicates onto the left-hand one's parameter before joining them:

{% raw %}
```csharp
public static class PredicateComposer
{
    public static Expression<Func<RateRequest, bool>> And(
        Expression<Func<RateRequest, bool>> left,
        Expression<Func<RateRequest, bool>> right)
    {
        var parameter = left.Parameters[0];
        var replacer = new ParameterReplacer(right.Parameters[0], parameter);
        var rightBody = replacer.Visit(right.Body)!;
        var body = Expression.AndAlso(left.Body, rightBody);
        return Expression.Lambda<Func<RateRequest, bool>>(body, parameter);
    }
}
```
{% endraw %}

`And` takes `left`'s own parameter as the one the composed rule will use,
rebinds `right`'s body onto that same parameter with `ParameterReplacer`,
joins the two bodies with `Expression.AndAlso`, the factory method for a
short-circuiting `&&`, and wraps the result in a new `LambdaExpression` with
`Expression.Lambda<Func<RateRequest, bool>>`, naming `parameter` as the
single parameter the compiled delegate will accept. `RewriteAndCompose
Tests.And_composes_two_predicates_over_one_parameter` asserts the composed
lambda's parameter is the exact same object as `left`'s, confirming the
splice landed on one shared parameter rather than two, and that its body's
`NodeType` is `ExpressionType.AndAlso`.
`And_composition_requires_both_conditions` is a `Theory` over all four
combinations of a heavy and a hazardous request, compiling the composed
predicate once per row and asserting it returns `true` only when both
conditions hold, the short-circuiting `&&` behavior `Expression.AndAlso`
produces carried all the way through to the compiled delegate.
`ParameterReplacer_rebinds_a_lambda_onto_a_new_parameter` checks the
rebinding in isolation, replacing `IsHeavy`'s parameter with a freshly
named one, rebuilding a lambda from the rebound body, and confirming the
rebuilt lambda still compiles to a delegate that reports `true` for a
twenty-five-kilogram request and `false` for a five-kilogram one, the
renamed parameter changing nothing about what the predicate computes.

## Build, run, observe

Every type in this example, `TariffExpressionLibrary`, `RateDescriber`,
`ReferencedPropertyCollector`, `RateInflator`, `ParameterReplacer`, and
`PredicateComposer`, builds, reads, rewrites, and compiles
`System.Linq.Expressions` trees synchronously and in memory, so the suite
needs nothing beyond the local .NET 10 SDK. The example is
standalone-testable from its own directory:

```sh
cd examples/26-expression-trees
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 13
  failed: 0
  succeeded: 13
  skipped: 0
  duration: 731ms
```

The thirteen cases split across the two halves of this example.
`DescribeAndInspectTests` proves the authoring-and-reading half: compiling
`WeightRate` and invoking it returns the expected decimal, `RateDescriber`
renders both a simple rule and a composed two-term sum as readable strings,
and `ReferencedPropertyCollector` finds the right property names from a
composed rule and from one that would throw if it were ever run.
`RewriteAndComposeTests` proves the rewriting-and-composing half:
`RateInflator` scales a rule's rate and leaves a string comparison
untouched, `ParameterReplacer` rebinds a lambda onto a fresh parameter
without changing what it computes, and `PredicateComposer.And`, checked
both for its structural shape and across a four-row `Theory` of heavy and
hazardous combinations, composes two independently written predicates into
one that requires both conditions.

## When not to use it

An expression tree earns nothing over a plain delegate when nothing ever
reads the tree before it runs. `TariffExpressionLibrary.WeightRate`'s
`Expression<Func<RateRequest, decimal>>` type only pays off because
`RateDescriber`, `ReferencedPropertyCollector`, and `RateInflator` each read
or rewrite that tree before anything calls `Compile()`. A rate rule that is
only ever going to be invoked, never described, never inspected for which
properties it depends on, and never scaled, gets nothing from being authored
as `Expression<Func<RateRequest, decimal>>` that a direct `Func<RateRequest,
decimal>` would not provide with less code and no tree to build first.

Building and compiling a tree both cost real time, separate from whatever
the compiled delegate eventually computes. Constructing the
`ParameterExpression`, `MemberExpression`, and `BinaryExpression` nodes
behind even a small rule allocates several objects before a single rate is
calculated, and `Expression<TDelegate>.Compile()` walks the finished tree
and emits a working delegate from it, work a cached, already-compiled
`Func<RateRequest, decimal>` never repeats. A rule compiled once and
invoked many times, which is how every test in this chapter's suite uses
`Compile()`, pays that cost a single time; a rule compiled fresh inside a
hot loop, once per request rather than once per distinct rule, pays it on
every call for no benefit a cached delegate would not already provide.

A tree built by composing several `ExpressionVisitor` rewrites, the way
`PredicateComposer.And` splices a rebound predicate onto another's
parameter, has no source line of its own to step into. Stepping through a
compiled delegate built this way lands a debugger inside generated code
with no method body in the project to follow, only whatever `Expression`
nodes the visitors assembled. A rule written directly as a method or a
single lambda stays readable under a debugger exactly because nothing
rewrote it first; a rule assembled from several composed trees is easier to
check by calling `RateDescriber.Describe` on the result and reading the
rendered string than by stepping through the compiled delegate itself.

<blockquote class="ai-sidebar" markdown="1">
Asked to add a user-configurable pricing rule, an AI
pairing partner will sometimes reach for a string-based formula parser,
tokenizing an expression and hand-writing a recursive-descent evaluator,
for a problem `Expression`'s factory methods and `Expression.Lambda<
TDelegate>(...).Compile()` already cover without a parser of any kind:
build the tree from known parts, as `PredicateComposer.And` does, and
compile it once. A related suggestion reaches for an
`Expression<Func<RateRequest, decimal>>` and a fresh `Compile()` call
inside a per-request rating loop, for a rule that is read once and run
once, paying the cost this chapter's "When not to use it" section
describes without ever using the tree for anything a `Func<RateRequest,
decimal>` could not do directly. Check a generated answer against what
this chapter built: reach for a tree when something, a describer, a
property collector, a rewrite, or a composition, needs to read or change
the computation before it runs, compile once and cache the delegate when
a rule is evaluated repeatedly, and keep a one-off calculation as a plain
delegate with no tree behind it at all.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/): the authority for `System.Linq.Expressions`, the lambda-to-tree conversion `TariffExpressionLibrary`'s properties rely on, and how a LINQ provider reads a tree rather than invoking a compiled delegate.
- Microsoft Learn, [ExpressionVisitor Class](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor): the authority for the base class `RateDescriber`, `ReferencedPropertyCollector`, `RateInflator`, and `ParameterReplacer` all derive from, and the default traversal that rebuilds a tree's ancestors when a nested `VisitXxx` override returns a new node.
- Microsoft Learn, [Anonymous function expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#anonymous-function-expressions): the language-specification authority for why a lambda converts to an `Expression<TDelegate>` tree rather than to executable code when its target type is an expression-tree type.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): `ExpressionVisitor`'s `VisitBinary`/`VisitMember`/`VisitConstant` overrides are the Visitor double-dispatch shape applied to a real, built-in class hierarchy of tree nodes.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
