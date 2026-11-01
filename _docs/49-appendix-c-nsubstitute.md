---
title: "NSubstitute reference"
order: 49
layout: tutorial
sectionid: docs
part: appendices
appendix: "C"
description: "A reference for NSubstitute, the mocking library this book uses, with a note on Moq for readers on existing Moq codebases."
---

## Learning objectives

- Create a substitute for an interface with `Substitute.For<T>()` and stub a
  return value with `.Returns(...)`.
- Stub different return values for different argument combinations, and stub
  a sequence of returns across consecutive calls.
- Verify interactions with `Received()`, `Received(n)`, `DidNotReceive()`, and
  `Received.InOrder`, including the `await`-qualified form for asynchronous
  members.
- Use the `Arg.Any<T>()` and `Arg.Is<T>(predicate)` matchers to loosen or
  tighten which calls a stub or verification applies to.
- Configure a substitute to throw, and recognize when NSubstitute is the
  right tool against a codebase that already standardizes on Moq.

## Why this matters

Every example in this book that has a collaborator worth isolating, a carrier
rate lookup, a tax policy, an audit sink, a clock, uses NSubstitute to stand
in for that collaborator. The library shows up in nearly every test project
under `examples/`, so a reader who has not used it before needs a place to
look up the handful of idioms that recur: creating a substitute, telling it
what to return, and checking what was called on it. This appendix collects
those idioms in one place, with the exact signatures this book's suites use,
so a chapter can show a test without re-explaining `Substitute.For` every
time.

NSubstitute's design goal is a mocking API that reads like plain C#: no
`Setup`, no `.Object` indirection, no expectation DSL to learn before the
first assertion. A substitute is a dynamic proxy generated at runtime from an
interface (or an unsealed class with virtual members), and every member on it
starts out doing nothing: a method returns the default value for its return
type, a `void` method does nothing, and a property returns its backing
field's default. Tests then narrow that behavior down to exactly the calls
they care about.

## Creating a substitute

`Substitute.For<T>()` is the one entry point. It takes a type parameter and
returns an object that implements `T`:

```csharp
using NSubstitute;

var taxPolicy = Substitute.For<ITaxPolicy>();
```

`taxPolicy` now satisfies `ITaxPolicy` well enough to pass to any constructor
that asks for one. Called with no further configuration, `ApplyTax` returns
`default(Money)`, not an exception and not a null-reference failure:

```csharp
var taxPolicy = Substitute.For<ITaxPolicy>();

var result = taxPolicy.ApplyTax(new Money(50.00m));

Assert.Equal(default, result);
```

That default-returning behavior matters: a substitute a test never configures
still behaves predictably, which is what lets a test pass three collaborators
into a constructor and only configure the one the test cares about.

## Stubbing returns

`.Returns(value)` attaches a canned return value to a specific call,
argument values and all:

```csharp
var taxPolicy = Substitute.For<ITaxPolicy>();
taxPolicy.ApplyTax(new Money(1.00m)).Returns(new Money(999.99m));

var result = taxPolicy.ApplyTax(new Money(1.00m));

Assert.Equal(new Money(999.99m), result);
```

The same substitute can carry different stubs for different arguments. Each
call to `.Returns(...)` records its own argument-to-result mapping, so a
test does not need a separate substitute per input:

```csharp
var taxPolicy = Substitute.For<ITaxPolicy>();
taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
taxPolicy.ApplyTax(new Money(200.00m)).Returns(new Money(220.00m));

Assert.Equal(new Money(110.00m), taxPolicy.ApplyTax(new Money(100.00m)));
Assert.Equal(new Money(220.00m), taxPolicy.ApplyTax(new Money(200.00m)));
```

When the call's result should depend on something other than a fixed value,
for example an incrementing counter, or when a test wants to assert on the
arguments a caller passed rather than supply a fixed answer, `.Returns` also
accepts a function instead of a value. The function receives a
`CallInfo` and can inspect the arguments the call was made with:

```csharp
references.NewReference().Returns(_ => $"RQ-{Guid.NewGuid():N}");
```

`.Returns` also accepts multiple values, and NSubstitute returns them in
order across successive calls, repeating the last one once the list is
exhausted. That consecutive-returns form is useful for a collaborator whose
answer changes call over call, a retry policy's gateway, for instance,
without reaching for a stateful fake:

```csharp
gateway.GetStatusAsync(trackingNumber, Arg.Any<CancellationToken>())
    .Returns(Task.FromResult("Pending"), Task.FromResult("InTransit"), Task.FromResult("Delivered"));
```

`ReturnsForAnyArgs(value)` is the broader sibling of `.Returns`: it stubs a
member regardless of which arguments it is called with, rather than binding
the stub to one specific argument list. Reach for it when a test does not
care what was passed at all, a counter or an identity generator, and reach
for the argument-specific `.Returns` (optionally combined with
`Arg.Is<T>(...)`, below) everywhere a wrong argument should be allowed to
fail the test.

## Verifying interactions

Stubbing answers what a substitute returns. Verifying checks what a caller
did with it. `Received()` asserts that a call happened at least once;
`Received(n)` asserts it happened exactly `n` times; `DidNotReceive()`
asserts it never happened. The book's `ShipmentRatingEngine` suite verifies
its audit sink this way:

```csharp
var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));
var consignment = new Consignment("C-1001", "Rotterdam", 300m);

await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

await auditSink.Received(1).RecordAsync("C-1001", new Money(110.00m), Arg.Any<CancellationToken>());
```

Note the `await` in front of `Received(1)`. `RecordAsync` returns a `Task`,
and verifying a call against an asynchronous member needs the same `await`
discipline as calling it: `Received(1)` returns an awaitable proxy, and
skipping the `await` lets the verification no-op without a test failure.

`DidNotReceive()` reads the same way, asserting the call's absence rather
than its presence:

```csharp
await auditSink.DidNotReceive().RecordAsync(
    Arg.Any<string>(), Arg.Is<Money>(quoted => quoted != new Money(110.00m)), Arg.Any<CancellationToken>());
```

`DidNotReceiveWithAnyArgs()` is the companion for a member whose exact
arguments are inconvenient to restate at the call site: it asserts the member
was never called at all, under any arguments, which is the right check after
an early return or a thrown exception should have short-circuited the rest of
a workflow:

```csharp
Assert.Throws<AddressValidationException>(() => facade.Book(request));
gateway.DidNotReceiveWithAnyArgs().GetQuote(default!);
filer.DidNotReceiveWithAnyArgs().File(default!, default!);
```

When the order of calls across multiple substitutes matters, not just that
each one happened, `Received.InOrder` wraps a block of call expressions and
asserts they happened in that sequence:

```csharp
await gateway.Received(1).ConfirmAsync("BK-6001", Arg.Any<CancellationToken>());
await gateway.Received(1).ConfirmAsync("BK-6002", Arg.Any<CancellationToken>());
Received.InOrder(() =>
{
    gateway.ConfirmAsync("BK-6001", Arg.Any<CancellationToken>());
    gateway.ConfirmAsync("BK-6002", Arg.Any<CancellationToken>());
});
```

The calls inside `Received.InOrder`'s delegate are not re-executed; NSubstitute
replays its recorded call history and checks the sequence matches.

## Argument matchers

`Arg.Any<T>()` loosens a stub or verification so it matches any value of
type `T` at that position, which is the common case for a `CancellationToken`
parameter a test does not control directly:

```csharp
carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
    .Returns(baseRate);
```

`Arg.Is<T>(predicate)` tightens the match instead, accepting a lambda that
the actual argument must satisfy:

```csharp
await auditSink.Received(1).RecordAsync(
    Arg.Is<string>(id => id.StartsWith("C-SANTOS", StringComparison.Ordinal)),
    Arg.Any<Money>(),
    Arg.Any<CancellationToken>());
```

Mixing `Arg.Is` and literal values in the same call is normal: a literal
argument means "exactly this value," `Arg.Any<T>()` means "any value of this
type," and `Arg.Is<T>(predicate)` means "any value matching this condition."
A stubbed or verified call can combine all three across its parameter list,
as the carrier-rate lookups above do with a literal destination, a literal
weight, and `Arg.Any<CancellationToken>()` for the token.

## Throwing substitutes

A substitute can be told to throw instead of return. For a `void` member,
`.When(...).Do(...)` runs a callback (here, one that throws) whenever the
matching call happens:

```csharp
validator
    .When(v => v.Validate(request))
    .Do(_ => throw new AddressValidationException("Address 'INVALID' is not a recognized shipping address."));

Assert.Throws<AddressValidationException>(() => facade.Book(request));
```

For a member with a return value, the equivalent is a `.Returns` overload
whose function throws instead of returning, or the `.Throws(exception)`
shorthand:

```csharp
carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
    .Throws(new CarrierUnavailableException("rate service unreachable"));
```

Both forms configure the substitute once; the exception fires on every call
that matches the configured arguments, which is what makes them suitable for
exercising a caller's error-handling path rather than its happy path.

## Asynchronous substitutes

NSubstitute has no separate API for asynchronous members. `.Returns(value)`
on a member that returns `Task<T>` wraps `value` in an already-completed
task, so the substitute can be awaited exactly like a real implementation:

```csharp
carrierRates.GetBaseRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
    .Returns(new Money(76.00m));

var baseRate = await carrierRates.GetBaseRateAsync(
    "Rotterdam", 820m, TestContext.Current.CancellationToken);

Assert.Equal(new Money(76.00m), baseRate);
```

`.Returns(Task.FromResult(value))` works too, and is equivalent; NSubstitute
accepts a bare value for a `Task<T>`-returning member and wraps it
automatically, so most of this book's suites use the shorter form and reserve
the explicit `Task.FromResult` for places where the test is already
constructing a task for another reason. The same rule from the verification
section applies here: `Received(n)` and `DidNotReceive()` need an `await` in
front of them when the member being checked is asynchronous, because the
verification itself returns an awaitable.

Every token-taking member in this book's examples threads a real
`CancellationToken` through, `TestContext.Current.CancellationToken` in most
tests, or a test-owned `CancellationTokenSource` when the test wants to prove
the token specifically reaches a collaborator:

```csharp
await carrierRates.Received(1).GetBaseRateAsync("Felixstowe", 12m, cancellationSource.Token);
await auditSink.Received(1).RecordAsync("C-2002", new Money(15.00m), cancellationSource.Token);
```

## A note on Moq

Some readers maintain an existing suite built on Moq, and this book does not
pretend that suite does not exist. Where a chapter mentions Moq at all, it is
to acknowledge that Moq's `Mock<T>`, `Setup`, and `.Object` accomplish the
same isolation NSubstitute does, with a different API shape. This book never
tells a team to migrate a working Moq suite to NSubstitute. A mocking library
is infrastructure, not a design pattern, and a suite that already passes
reliably on Moq has nothing to gain from a rewrite whose only output is a
different import statement. NSubstitute is this book's choice for its own
examples because its API has no `Setup`/`.Object` ceremony to explain before
the first real test, which keeps the testing appendix and the pattern
chapters focused on the collaborator being isolated rather than the mocking
library isolating it.

## For deeper coverage

- The official NSubstitute documentation at
  [nsubstitute.github.io](https://nsubstitute.github.io/) covers the full API
  surface, including configuring substitutes for classes with virtual
  members, partial substitutes, and the auto- and recursive-substitute
  helpers this appendix does not cover because the book's examples do not use
  them.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
