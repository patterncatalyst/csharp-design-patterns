---
title: "NSubstitute reference"
order: 49
layout: tutorial
sectionid: docs
part: appendices
appendix: "C"
description: "A reference for NSubstitute, the mocking library the examples use, with a note on Moq for readers on existing Moq codebases."
---

## Learning objectives

By the end of this appendix you can:

- Create a substitute with `Substitute.For<T>()` and stub a return value with
  `.Returns(...)`.
- Stub different returns for different arguments, and a sequence of returns
  across consecutive calls.
- Verify interactions with `Received()`, `Received(n)`, `DidNotReceive()` and
  `Received.InOrder`, including the awaited form for async members.
- Loosen or tighten a match with `Arg.Any<T>()` and `Arg.Is<T>(predicate)`.
- Make a substitute throw, and decide whether NSubstitute fits a codebase
  that already uses Moq.

## Why this matters

Collaborators worth isolating, such as a carrier rate lookup, a tax policy,
an audit sink or a clock, are replaced with NSubstitute in nearly every test
project under `examples/`. The same few idioms recur: create a substitute,
tell it what to return, check what was called.

NSubstitute's API reads like plain C#: no `Setup`, no `.Object`, no
expectation DSL. A substitute is a dynamic proxy generated at run time from
an interface (or an unsealed class with virtual members). Every member starts
out doing nothing: a method returns its return type's default, a `void`
method does nothing, a property returns its default. You then configure only
the calls you care about.

## API summary

| Need | API |
| --- | --- |
| Create | `Substitute.For<T>()` |
| Fixed return for given arguments | `.Returns(value)` |
| Computed return | `.Returns(callInfo => ...)` |
| Consecutive returns | `.Returns(first, second, third)` |
| Return for any arguments | `.ReturnsForAnyArgs(value)` |
| Called at least once / exactly n | `Received()`, `Received(n)` |
| Never called with these arguments | `DidNotReceive()` |
| Never called at all | `DidNotReceiveWithAnyArgs()` |
| Call order | `Received.InOrder(() => ...)` |
| Match any value / a condition | `Arg.Any<T>()`, `Arg.Is<T>(predicate)` |
| Throw from a `void` member | `.When(...).Do(_ => throw ...)` |
| Throw from a returning member | `.Throws(exception)` |

## Creating a substitute

`Substitute.For<T>()` is the one entry point. It returns an object that
implements `T`:

```csharp
using NSubstitute;

var taxPolicy = Substitute.For<ITaxPolicy>();
```

An unconfigured `ApplyTax` returns `default(Money)`, not an exception or a
null-reference failure:

```csharp
var taxPolicy = Substitute.For<ITaxPolicy>();

var result = taxPolicy.ApplyTax(new Money(50.00m));

Assert.Equal(default, result);
```

So you can pass three substitutes into a constructor and configure only the
one that matters.

## Stubbing returns

`.Returns(value)` attaches a canned result to a call with specific argument
values:

```csharp
var taxPolicy = Substitute.For<ITaxPolicy>();
taxPolicy.ApplyTax(new Money(1.00m)).Returns(new Money(999.99m));

var result = taxPolicy.ApplyTax(new Money(1.00m));

Assert.Equal(new Money(999.99m), result);
```

One substitute can carry a stub per argument list, so you don't need a
substitute per input:

```csharp
var taxPolicy = Substitute.For<ITaxPolicy>();
taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
taxPolicy.ApplyTax(new Money(200.00m)).Returns(new Money(220.00m));

Assert.Equal(new Money(110.00m), taxPolicy.ApplyTax(new Money(100.00m)));
Assert.Equal(new Money(220.00m), taxPolicy.ApplyTax(new Money(200.00m)));
```

When the result depends on something other than a fixed value, such as a
counter, or you need the arguments the caller passed, `.Returns` takes a
function. It receives a `CallInfo` holding the call's arguments:

```csharp
references.NewReference().Returns(_ => $"RQ-{Guid.NewGuid():N}");
```

Given several values, `.Returns` hands them out in order across successive
calls and repeats the last one once the list runs out. That covers a
collaborator whose answer changes call over call, such as a gateway behind a
retry policy, without a stateful fake:

```csharp
gateway.GetStatusAsync(trackingNumber, Arg.Any<CancellationToken>())
    .Returns(Task.FromResult("Pending"), Task.FromResult("InTransit"), Task.FromResult("Delivered"));
```

`ReturnsForAnyArgs(value)` stubs a member whatever its arguments. Use it when
the arguments don't matter, as with a counter or an identity generator. Use
the argument-specific `.Returns`, optionally with `Arg.Is<T>(...)`, wherever
a wrong argument should fail.

## Verifying interactions

Stubbing sets what a substitute returns; verifying checks what the caller did
with it. `Received()` requires at least one matching call, `Received(n)`
exactly `n`, and `DidNotReceive()` none. Here the audit sink must have
recorded the quote once:

```csharp
var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));
var consignment = new Consignment("C-1001", "Rotterdam", 300m);

await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

await auditSink.Received(1).RecordAsync("C-1001", new Money(110.00m), Arg.Any<CancellationToken>());
```

`RecordAsync` returns a `Task`, so the verification is awaited like the call
itself.

`DidNotReceive()` asserts absence with the same shape:

```csharp
await auditSink.DidNotReceive().RecordAsync(
    Arg.Any<string>(), Arg.Is<Money>(quoted => quoted != new Money(110.00m)), Arg.Any<CancellationToken>());
```

`DidNotReceiveWithAnyArgs()` asserts the member was never called under any
arguments. It's the check after an early return or a thrown exception should
have stopped the rest of a workflow:

```csharp
Assert.Throws<AddressValidationException>(() => facade.Book(request));
gateway.DidNotReceiveWithAnyArgs().GetQuote(default!);
filer.DidNotReceiveWithAnyArgs().File(default!, default!);
```

`Received.InOrder` takes a block of call expressions and requires that they
happened in that sequence:

```csharp
await gateway.Received(1).ConfirmAsync("BK-6001", Arg.Any<CancellationToken>());
await gateway.Received(1).ConfirmAsync("BK-6002", Arg.Any<CancellationToken>());
Received.InOrder(() =>
{
    gateway.ConfirmAsync("BK-6001", Arg.Any<CancellationToken>());
    gateway.ConfirmAsync("BK-6002", Arg.Any<CancellationToken>());
});
```

The calls inside the delegate aren't executed again. NSubstitute checks them
against its recorded call history.

## Argument matchers

| Argument | Matches |
| --- | --- |
| A literal | Exactly that value |
| `Arg.Any<T>()` | Any value of type `T` |
| `Arg.Is<T>(predicate)` | Any value satisfying the predicate |

One call can combine all three across its parameters. `Arg.Any<T>()` is the
usual choice for a `CancellationToken` you don't control:

```csharp
carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
    .Returns(baseRate);
```

`Arg.Is<T>(predicate)` narrows the match:

```csharp
await auditSink.Received(1).RecordAsync(
    Arg.Is<string>(id => id.StartsWith("C-SANTOS", StringComparison.Ordinal)),
    Arg.Any<Money>(),
    Arg.Any<CancellationToken>());
```

## Throwing substitutes

For a `void` member, `.When(...).Do(...)` runs a callback, here one that
throws, on every matching call:

```csharp
validator
    .When(v => v.Validate(request))
    .Do(_ => throw new AddressValidationException("Address 'INVALID' is not a recognized shipping address."));

Assert.Throws<AddressValidationException>(() => facade.Book(request));
```

For a member with a return value, use `.Throws(exception)` or a `.Returns`
function that throws:

```csharp
carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
    .Throws(new CarrierUnavailableException("rate service unreachable"));
```

Either way the exception fires on every matching call, which is how you drive
a caller's error-handling path.

## Asynchronous substitutes

There's no separate async API. `.Returns(value)` on a `Task<T>` member wraps
`value` in a completed task, so the substitute is awaited like a real
implementation:

```csharp
carrierRates.GetBaseRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
    .Returns(new Money(76.00m));

var baseRate = await carrierRates.GetBaseRateAsync(
    "Rotterdam", 820m, TestContext.Current.CancellationToken);

Assert.Equal(new Money(76.00m), baseRate);
```

`.Returns(Task.FromResult(value))` is equivalent; use the explicit form only
when you're already building a task for another reason. `Received(n)` and
`DidNotReceive()` on an async member are awaited, as above.

Token-taking members get a real `CancellationToken`:
`TestContext.Current.CancellationToken` in most tests, or the token of your
own `CancellationTokenSource` when the point is that the token reaches a
collaborator:

```csharp
await carrierRates.Received(1).GetBaseRateAsync("Felixstowe", 12m, cancellationSource.Token);
await auditSink.Received(1).RecordAsync("C-2002", new Money(15.00m), cancellationSource.Token);
```

## A note on Moq

Moq's `Mock<T>`, `Setup` and `.Object` give the same isolation with a
different API shape. Don't migrate a working Moq suite to NSubstitute: a
mocking library is infrastructure, not a design pattern, and the rewrite buys
nothing but a different import. NSubstitute is used here because it has no
`Setup` or `.Object` ceremony to explain before the first test.

## For deeper coverage

- The official NSubstitute documentation at
  [nsubstitute.github.io](https://nsubstitute.github.io/) covers the full API
  surface, including configuring substitutes for classes with virtual
  members, partial substitutes, and the auto- and recursive-substitute
  helpers the examples don't use.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
