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

- Author a tariff rule as an `Expression<Func<RateRequest, decimal>>` and say what the compiler builds instead of a method body.
- Read a tree without running it, with `ExpressionVisitor` subclasses that describe a rule and collect the properties it touches.
- Rewrite a tree into a new one, scaling only its `decimal` constants.
- Join two predicates over one shared parameter with `ParameterReplacer` and `PredicateComposer.And`, then compile the result.

## Why this matters

A lambda normally compiles to executable code: `r => r.WeightKilograms * 2.50m` becomes a method body. Give the same lambda the target type `Expression<Func<RateRequest, decimal>>` and the compiler builds a tree of `Expression` objects describing the multiplication, the property read and the constant. The text is identical; the output is data.

That's how a LINQ provider translates a query instead of running it. A query operator that takes `Expression<Func<TSource, TResult>>` hands the provider the tree, which it walks to find property reads and comparisons and rebuilds in its own target language. A `Func` is a black box the runtime can call but not read. An expression tree can be described, inspected, rewritten or compiled, in whatever order the job needs.

{% include excalidraw.html name="26-expression-trees" caption="Figure 26.1: A tariff rate rule compiles to a tree of Expression nodes rather than executable code. RateDescriber reads that tree and renders it as a string without running it, while Compile takes the same tree down a different path into a runnable delegate." %}

## Authoring a rule as data

A rate request carries the facts a rule reads:

```csharp
public sealed record RateRequest(decimal WeightKilograms, decimal DeclaredValue, string Destination, bool Hazardous);
```

Each rule is an `Expression` property:

```csharp
public static class TariffExpressionLibrary
{
    public static Expression<Func<RateRequest, decimal>> WeightRate => r => r.WeightKilograms * 2.50m;

    public static Expression<Func<RateRequest, decimal>> ValueRate => r => r.DeclaredValue * 0.015m;

    public static Expression<Func<RateRequest, bool>> IsHeavy => r => r.WeightKilograms > 20m;
}
```

For `WeightRate` the compiler builds a `ParameterExpression` named `r`, a `MemberExpression` reading `WeightKilograms`, a `ConstantExpression` holding `2.50m`, and a `BinaryExpression` of type `Multiply`, all inside a `LambdaExpression`. `IsHeavy` gets a `GreaterThan` node instead. The target type, not the body, decides whether a lambda becomes a tree or a delegate.

`WeightRate.Compile()` still returns a working delegate: a 10 kg request rates at 25.00. Everything below happens before that call.

## Reading a tree without running it

`ExpressionVisitor` is the base class for walking a tree. `Visit` dispatches each node to the matching `VisitXxx` override, and every node kind without an override falls through to a default that recurses into its children. `RateDescriber` overrides four of them to render a rule as text:

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

`WeightRate` renders as `(r.WeightKilograms * 2.50)`. A two-term sum renders as `((r.WeightKilograms * 2.50) + (r.DeclaredValue * 0.015))`: `VisitBinary` recurses through `Visit`, so nesting depth needs no special handling. Nothing is compiled or invoked.

### Inspecting before translating

`ReferencedPropertyCollector` reads which `RateRequest` properties a rule depends on, which is the question a provider asks before it maps a filter to a column:

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

Calling `base.VisitMember` keeps the default traversal going into nested nodes. The two-term sum yields `WeightKilograms` and `DeclaredValue`.

Reading isn't running. `r => r.WeightKilograms / 0m` throws `DivideByZeroException` when compiled and invoked, but `Collect` on its tree returns `WeightKilograms` without error. A provider relies on the same property: it never runs a `Where` predicate against an in-memory object to learn what it compares.

{% include excalidraw.html name="26-inspecting-a-tree" caption="Figure 26.3: ReferencedPropertyCollector walks a rule's tree and records which properties it touches without ever executing it, the way a LINQ provider reads a query before translating it. Compile takes the same tree down the other path into a runnable delegate." %}

## Rewriting a tree

`RateInflator` builds a new tree with every `decimal` constant scaled:

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

`Expression` nodes are immutable, so `VisitConstant` returns a new node rather than changing the old one. The default traversal then rebuilds each ancestor of a changed node, so the result shares every untouched node with the original. Inflating `WeightRate` by 1.10 makes a 10 kg request rate at 27.5.

The type check on `node.Value` keeps the rewrite scoped. In `r.Destination == "Remote" ? r.WeightKilograms * 2.50m : r.WeightKilograms * 2.00m`, the string `"Remote"` is also a `ConstantExpression`. Inflating by 2 gives 50 for a remote 10 kg request and 40 otherwise, and the comparison still matches.

{% include excalidraw.html name="26-expressionvisitor-rewrite" caption="Figure 26.2: RateInflator rewrites a tree by scaling its decimal constants, returning a brand-new tree because expression trees are immutable. PredicateComposer rebinds one predicate's parameter onto another's and joins both bodies into a single lambda over one shared parameter." %}

## Building trees with the factory API

Joining two predicates has no lambda syntax, so it uses the `Expression` factory methods. `IsHeavy` and `r => r.Hazardous` each have their own `ParameterExpression` named `r`. Same name, different objects: joining the two bodies directly with `Expression.AndAlso` gives a tree that refers to two parameters and can't be compiled as a one-parameter lambda. `ParameterReplacer` rebinds one onto the other first:

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

The composed lambda's single parameter is `left`'s own object, and its body is an `AndAlso` node, the short-circuiting `&&`. Compiled, it returns `true` only for a request that's both heavy and hazardous. Rebinding alone changes nothing about what a predicate computes: `IsHeavy` moved onto a fresh parameter still reports `true` at 25 kg and `false` at 5 kg.

## When not to use it

- **Nothing reads the tree.** `Expression<Func<...>>` pays off only when something describes, inspects, rewrites or composes the rule before it runs. A rule that's only ever invoked should be a `Func<RateRequest, decimal>`.
- **Compiling per call.** Building nodes allocates, and `Compile()` emits a delegate from the tree. Compile once per distinct rule and cache the delegate; compiling inside a per-request loop repeats that cost on every call.
- **Debugging composed trees.** A delegate built from several rewrites has no source line to step into. Check it by rendering the result with `RateDescriber.Describe` instead.

> **🤖 Explore with your agent**
>
> "Keep each rule in `TariffExpressionLibrary` as an `Expression<Func<RateRequest, decimal>>` or `Expression<Func<RateRequest, bool>>` written as a lambda, never parsed from a string. In `PredicateComposer.And`, rebind the right lambda onto the left one's parameter with `ParameterReplacer` before `Expression.AndAlso`. Rewrite constants in an `ExpressionVisitor` such as `RateInflator`, and read the properties a rule touches with `ReferencedPropertyCollector` without running it. Call `Compile()` once per distinct rule and keep the delegate. Use a plain `Func` for any rule nothing inspects or rewrites."
>
> Run `dotnet test --project TariffExpressions.Tests --filter RewriteAndComposeTests`; a tokenizer in the reply is the defect.

## Build, run, observe

```sh
cd examples/26-expression-trees
dotnet test
```

All 13 tests pass; `scripts/verify.sh` runs them with every other example. One experiment:

- **Skip the rebinding.** In `PredicateComposer.And`, join `right.Body` directly instead of `rightBody`. Compiling the composed predicate throws `InvalidOperationException`, because `right`'s parameter isn't in scope.

## For deeper coverage

- Microsoft Learn, [Expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/): `System.Linq.Expressions`, the lambda-to-tree conversion, and how a LINQ provider reads a tree rather than invoking a delegate.
- Microsoft Learn, [ExpressionVisitor Class](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor): the base class and the default traversal that rebuilds ancestors when an override returns a new node.
- Microsoft Learn, [Anonymous function expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#anonymous-function-expressions): the language rule that converts a lambda to an `Expression<TDelegate>` when the target type is an expression-tree type.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): `ExpressionVisitor`'s overrides are Visitor's double dispatch applied to a built-in hierarchy of tree nodes.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
