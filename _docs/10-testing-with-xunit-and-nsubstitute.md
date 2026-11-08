---
title: "Proving it works: xUnit and NSubstitute"
order: 10
layout: tutorial
sectionid: docs
part: "dotnet-substrate"
description: "The testing baseline every later chapter uses, with xUnit for test structure and NSubstitute for collaborator substitution."
---

<!-- EXAMPLE (shipping domain): The rating engine under xUnit and NSubstitute. -->

## Learning objectives

By the end of this chapter you can:

- Write a four-phase `[Fact]`, Arrange, Act, Assert, against a class with asynchronous collaborators.
- Parametrize with `[Theory]` and `[InlineData]`, and say why a `decimal` case arrives as a `double` literal.
- Build a `TheoryData<T1, T2>` source for `[MemberData]` and say which analyzer rules an `IEnumerable<object[]>` trips.
- Create a double with `Substitute.For<T>()`, stub it with `.Returns(...)` per argument, and say what an unstubbed call returns.
- Verify async calls with `await ...Received(1)...` and `DidNotReceive()`, narrow them with `Arg.Any<T>()` and `Arg.Is<T>(predicate)`, and thread a cancellation token the way xUnit1051 requires.

## Why this matters

A `ShipmentRatingEngine` quotes a consignment by calling a carrier's rate API, applying a jurisdiction's tax rules and writing a compliance record. A unit test can't touch any of the three: the carrier may be down or rate limited, the tax data may not be loaded, and an audit log must never be written by a test run. What's under test is the wiring: a base rate goes through the tax policy and into an audit record.

xUnit supplies the structure: `[Fact]` for one invariant, `[Theory]` for one invariant over several inputs, and a runner that reports each case. NSubstitute supplies the collaborators: `Substitute.For<ICarrierRates>()` answers instantly with what it's told to and remembers every call, so a test can ask what it received. No container, service or shared fixture is involved, and sixteen tests run in under a second with `dotnet test`. That speed is what makes running them on every refactoring step practical.

{% include excalidraw.html name="10-testing-with-xunit-and-nsubstitute" caption="Figure 10.1: A four-phase test arranges substitute doubles for three collaborators, acts by calling the method once, then asserts both the result and which doubles were called." %}

## The four-phase test: Arrange, Act, Assert

`ShipmentRatingEngine` takes three constructor-injected collaborators and combines them in one method:

```csharp
public sealed class ShipmentRatingEngine(ICarrierRates carrierRates, ITaxPolicy taxPolicy, IAuditSink auditSink)
{
    public async Task<Money> QuoteAsync(Consignment consignment, CancellationToken cancellationToken)
    {
        var baseRate = await carrierRates
            .GetBaseRateAsync(consignment.Destination, consignment.Kilograms, cancellationToken)
            .ConfigureAwait(false);

        var quoted = taxPolicy.ApplyTax(baseRate);

        await auditSink
            .RecordAsync(consignment.ConsignmentId, quoted, cancellationToken)
            .ConfigureAwait(false);

        return quoted;
    }
}
```

The canonical shape, with each phase marked:

```csharp
[Fact]
public async Task Quotes_the_base_rate_with_tax_applied()
{
    // Arrange
    var carrierRates = Substitute.For<ICarrierRates>();
    carrierRates.GetBaseRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
        .Returns(new Money(100.00m));
    var taxPolicy = Substitute.For<ITaxPolicy>();
    taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
    var auditSink = Substitute.For<IAuditSink>();
    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
    var consignment = new Consignment("C-0001", "Rotterdam", 820m);

    // Act
    var quote = await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(new Money(110.00m), quote);
}
```

Arrange builds every collaborator and stubs what each returns. The audit sink gets no stub, because `QuoteAsync` reads nothing from it. Act is one call. Assert checks one outcome: the quote is the taxed `110.00`, not the carrier's `100.00`. Microsoft's [unit-testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices) name the same Arrange, Act, Assert shape, so "a reader can clearly separate what is being tested from the Arrange and Assert tasks," and advise against several Act phases in one test, because a failure then doesn't say which action broke.

`TestContext.Current` is xUnit v3's per-test context. Its `CancellationToken` is wired to each test's own cancellation, including a configured timeout, so passing it to every token-accepting call costs nothing and lets the runner stop a test that runs too long.

## [Theory] and [InlineData]: parametrizing with literal cases

`[Theory]` runs one method once per row of data, and `[InlineData]` supplies literal rows on the attribute:

```csharp
[Theory]
[InlineData("Rotterdam", 100.0)]
[InlineData("Santos", 250.0)]
[InlineData("Busan", 900.0)]
public async Task Passes_the_destination_and_weight_through_to_the_carrier_rate_lookup(
    string destination, decimal kilograms)
{
    var carrierRates = Substitute.For<ICarrierRates>();
    carrierRates.GetBaseRateAsync(destination, kilograms, Arg.Any<CancellationToken>())
        .Returns(new Money(50.00m));
    var taxPolicy = Substitute.For<ITaxPolicy>();
    taxPolicy.ApplyTax(new Money(50.00m)).Returns(new Money(50.00m));
    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
    var consignment = new Consignment("C-0002", destination, kilograms);

    await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

    await carrierRates.Received(1).GetBaseRateAsync(destination, kilograms, Arg.Any<CancellationToken>());
}
```

The [xUnit v3 getting-started guide](https://xunit.net/docs/getting-started/v3/getting-started) says "theories are tests which are only true for a particular set of data," and the runner reports each row as its own result.

`100.0`, `250.0` and `900.0` are `double` literals bound to a `decimal` parameter. Attribute arguments must be compile-time constants of a type metadata can hold, and `decimal` isn't one, so `[InlineData("Rotterdam", 100.0m)]` doesn't compile. The `double` value converts to `decimal` when the parameter binds, without loss for these values. Every `[InlineData]`-driven theory with a `decimal` parameter hits the same constraint.

## Typed TheoryData: member data that satisfies the analyzers

Once a case needs a readable name or more than a couple of scalars, `[MemberData]` points the theory at a static member:

```csharp
public static TheoryData<decimal, decimal> WeightTierBaseRates() => new()
{
    { 10m, 25.00m },
    { 500m, 625.00m },
    { 2000m, 2400.00m },
};

[Theory]
[MemberData(nameof(WeightTierBaseRates))]
public async Task Returns_the_carriers_quoted_base_rate_for_each_weight_tier(
    decimal kilograms, decimal baseRateAmount)
{
    var carrierRates = Substitute.For<ICarrierRates>();
    carrierRates.GetBaseRateAsync("Rotterdam", kilograms, Arg.Any<CancellationToken>())
        .Returns(new Money(baseRateAmount));
    var taxPolicy = Substitute.For<ITaxPolicy>();
    taxPolicy.ApplyTax(new Money(baseRateAmount)).Returns(new Money(baseRateAmount));
    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
    var consignment = new Consignment("C-0003", "Rotterdam", kilograms);

    var quote = await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

    Assert.Equal(new Money(baseRateAmount), quote);
}
```

The member body is ordinary C# run at discovery, so a `decimal` literal is just a `decimal`. The return type matters more. Under warnings as errors, the xUnit analyzers reject the untyped shape:

| Rule | Catches |
| --- | --- |
| [xUnit1019](https://xunit.net/xunit.analyzers/rules/xUnit1019), "MemberData must reference a member providing a valid data type" | A member the runner can't enumerate into rows, which would otherwise fail only at run time. |
| [xUnit1042](https://xunit.net/xunit.analyzers/rules/xUnit1042), "The member referenced by the MemberData attribute returns untyped data rows" | An `IEnumerable<object[]>` source, where a row with the wrong count or order compiles and fails on invocation. |

`TheoryData<decimal, decimal>` pins every row to two `decimal` values in that order, so a malformed row is a compile error.

## NSubstitute basics: Substitute.For and .Returns

`Substitute.For<ITaxPolicy>()` returns an object that implements `ITaxPolicy`, answers every call with a default until told otherwise and records every call. The [NSubstitute getting-started guide](https://nsubstitute.github.io/help/getting-started/) builds the library around that one factory: no base class, no mock repository and no `.Object` property between the substitute and its interface.

Stubs are scoped to arguments, so one substitute can answer different inputs differently:

```csharp
[Fact]
public void A_substituted_tax_policy_can_stub_different_returns_for_different_arguments()
{
    var taxPolicy = Substitute.For<ITaxPolicy>();
    taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
    taxPolicy.ApplyTax(new Money(200.00m)).Returns(new Money(220.00m));

    Assert.Equal(new Money(110.00m), taxPolicy.ApplyTax(new Money(100.00m)));
    Assert.Equal(new Money(220.00m), taxPolicy.ApplyTax(new Money(200.00m)));
}
```

Per the [return-value documentation](https://nsubstitute.github.io/help/set-return-value/), the call `taxPolicy.ApplyTax(...)` runs against the substitute, which records the arguments, and `.Returns(...)` attaches the stub to that recorded call. Later calls match whichever stub's arguments match, with no hand-written lookup.

A member nobody stubbed returns its type's default:

```csharp
[Fact]
public void An_unstubbed_call_on_a_substitute_returns_the_types_default_value()
{
    var taxPolicy = Substitute.For<ITaxPolicy>();

    var result = taxPolicy.ApplyTax(new Money(50.00m));

    Assert.Equal(default, result);
}
```

`Money` is a `readonly record struct`, so `default` is `Amount == 0m`. An unstubbed substitute doesn't throw the way a strict mock does. A forgotten stub fails on the assertion with a `0m`, an empty string or a `null`, and that signature almost always means a missing stub, not a substitute bug.

## Verifying interactions: Received, DidNotReceive, and Arg matchers

Stubbing answers what a collaborator hands back; verification answers whether the class called it, with what and how often. `IAuditSink` is where that matters, because the engine reads nothing from it: the contract is one `RecordAsync` call with the right ID and amount.

```csharp
[Fact]
public async Task Records_exactly_one_audit_entry_for_the_quoted_consignment()
{
    var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));
    var consignment = new Consignment("C-1001", "Rotterdam", 300m);

    await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

    await auditSink.Received(1).RecordAsync("C-1001", new Money(110.00m), Arg.Any<CancellationToken>());
}
```

The [received-calls documentation](https://nsubstitute.github.io/help/received-calls/) describes `Received()` as checking "that a specific call has been received by a substitute," and the count narrows "at least once" to an exact number. An engine that recorded the same quote twice, say from a retry loop, fails even though its quote is correct.

`Arg.Any<CancellationToken>()` is a matcher that, per the [argument-matchers documentation](https://nsubstitute.github.io/help/argument-matchers/), ignores "the actual argument" at that position. `DidNotReceive()` is the negative form, and with `Arg.Is<T>(predicate)` it rules out a category of calls:

```csharp
[Fact]
public async Task Never_records_any_quote_other_than_the_one_it_returned()
{
    var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));
    var consignment = new Consignment("C-1002", "Santos", 50m);

    await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

    await auditSink.DidNotReceive().RecordAsync(
        Arg.Any<string>(), Arg.Is<Money>(quoted => quoted != new Money(110.00m)), Arg.Any<CancellationToken>());
}
```

The predicate says the sink never recorded any amount other than the correct quote. `Arg.Any<Money>()` couldn't express that, because it would match the correct quote too. The same predicate form can check that a recorded consignment ID starts with an expected prefix without pinning one literal.

| Matcher | Use it when |
| --- | --- |
| `Arg.Any<T>()` | The position doesn't matter to this check. |
| `Arg.Is<T>(predicate)` | One property of the argument matters. |
| A literal value | The exact value matters. |

{% include excalidraw.html name="10-nsubstitute-stub-vs-verify" caption="Figure 10.2: A substitute's two jobs, stubbing a return value on Arrange and verifying a call on Assert, with Arg matchers working on both sides." %}

## Async substitutes and cancellation tokens

`GetBaseRateAsync` and `RecordAsync` return `Task`s, and `.Returns(new Money(76.00m))` takes the plain value: the substitute wraps it in a completed `Task<Money>`, and the caller awaits it as it would the real integration.

Verifying an awaited call needs an `await` of its own:

```csharp
[Fact]
public async Task Verifying_an_awaited_call_needs_an_await_in_front_of_received()
{
    var carrierRates = Substitute.For<ICarrierRates>();
    carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
        .Returns(new Money(40.00m));
    var taxPolicy = Substitute.For<ITaxPolicy>();
    taxPolicy.ApplyTax(new Money(40.00m)).Returns(new Money(44.00m));
    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
    var consignment = new Consignment("C-2001", "Busan", 640m);

    await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

    await carrierRates.Received(1).GetBaseRateAsync("Busan", 640m, Arg.Any<CancellationToken>());
}
```

Without the `await` in front of `Received(1)`, the line still compiles, because its type is a `Task`. The method moves on without observing the result, and a mismatch slips past or shows up as an unobserved-task warning instead of a failure. Every `Received(...)` or `DidNotReceive()` on a `Task`-returning member needs its `await`.

Cancellation has to reach every downstream call:

```csharp
[Fact]
public async Task Passes_the_callers_cancellation_token_through_every_await()
{
    var carrierRates = Substitute.For<ICarrierRates>();
    carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
        .Returns(new Money(15.00m));
    var taxPolicy = Substitute.For<ITaxPolicy>();
    taxPolicy.ApplyTax(new Money(15.00m)).Returns(new Money(15.00m));
    var auditSink = Substitute.For<IAuditSink>();
    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
    using var cancellationSource = new CancellationTokenSource();
    var consignment = new Consignment("C-2002", "Felixstowe", 12m);

    await engine.QuoteAsync(consignment, cancellationSource.Token);

    await carrierRates.Received(1).GetBaseRateAsync("Felixstowe", 12m, cancellationSource.Token);
    await auditSink.Received(1).RecordAsync("C-2002", new Money(15.00m), cancellationSource.Token);
}
```

A caller-owned `CancellationTokenSource` lets both checks require that exact token at `GetBaseRateAsync` and at `RecordAsync`. `QuoteAsync` never creates its own token, swaps in `CancellationToken.None` or drops the token before the audit call. A consumer cancelling a batch of quotes expects every downstream call to stop, and an engine that dropped the token would leave an audit write running. [xUnit1051](https://xunit.net/xunit.analyzers/rules/xUnit1051), "calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken," enforces this in test code: under warnings as errors, a token-accepting call in a test body without a real token, `TestContext.Current.CancellationToken` or an explicit one, fails the build.

## NSubstitute versus Moq

Moq is the other common library, and a team with a working Moq suite has no reason to migrate it. Moq wraps the type in a `Mock<T>`, configures it through `.Setup(...)` and hands the code under test `.Object`: `var mock = new Mock<ITaxPolicy>(); mock.Setup(t => t.ApplyTax(It.IsAny<Money>())).Returns(...); var taxPolicy = mock.Object;`. NSubstitute returns the `ITaxPolicy` directly and attaches `.Returns(...)` to the call expression.

The other reason is supply chain. Moq 4.20 shipped SponsorLink, a closed-source component that collected a hashed form of the developer's email address at build time, with limited notice. After community pushback the maintainers reverted the default; the mechanism remains available as an opt-in. NSubstitute has no comparable telemetry dependency to evaluate. For a new project with no investment either way, that makes NSubstitute the default.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a test.** "Write a mocking example in C#" leaves open the library, the token and how async calls are stubbed and verified. State them:

> Write an xUnit v3 test for `ShipmentRatingEngine.QuoteAsync` with NSubstitute: `Substitute.For<T>()` for `ICarrierRates`, `ITaxPolicy` and `IAuditSink`, `.Returns(value)` on async members with no `Task.FromResult`, `TestContext.Current.CancellationToken` on every token-accepting call, and `await auditSink.Received(1).RecordAsync(...)` for the audit check. Use `TheoryData<,>` for member data.

Reject a reply that uses `Mock<T>` and `.Object` or passes `CancellationToken.None`.
</blockquote>

## Build, run, observe

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

All 16 tests pass in under a second, and `scripts/verify.sh` runs them with every other example. They cover one four-phase fact, six theory rows, three stubbing behaviors, three verifications and three async cases.


## For deeper coverage

- The official xUnit v3 documentation: [Getting Started with
  xUnit.net](https://xunit.net/docs/getting-started/v3/getting-started) for
  `[Fact]` and `[Theory]`, and the analyzer rule pages
  [xUnit1019](https://xunit.net/xunit.analyzers/rules/xUnit1019),
  [xUnit1042](https://xunit.net/xunit.analyzers/rules/xUnit1042), and
  [xUnit1051](https://xunit.net/xunit.analyzers/rules/xUnit1051) behind the
  typed-`TheoryData` and cancellation-token guidance.
- The official NSubstitute documentation: [Getting
  started](https://nsubstitute.github.io/help/getting-started/), [Setting a
  return value](https://nsubstitute.github.io/help/set-return-value/),
  [Checking received
  calls](https://nsubstitute.github.io/help/received-calls/), and [Argument
  matchers](https://nsubstitute.github.io/help/argument-matchers/).
- Microsoft Learn: [Best practices for writing unit
  tests](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
  for the Arrange-Act-Assert structure and test-double terminology, and the
  [`dotnet test`
  command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test).
- Gerard Meszaros, [*xUnit Test Patterns: Refactoring Test
  Code*](https://www.informit.com/store/xunit-test-patterns-refactoring-test-code-9780132800051)
  (Addison-Wesley): the source of the four-phase test and the
  taxonomy of test doubles, stub versus mock.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
