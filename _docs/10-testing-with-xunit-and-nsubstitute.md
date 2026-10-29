---
title: "Proving it works: xUnit and NSubstitute"
order: 10
layout: tutorial
sectionid: docs
part: "dotnet-substrate"
description: "The testing baseline every later chapter uses, with xUnit for the suite and NSubstitute for collaborator substitution."
---

<!-- EXAMPLE (shipping domain): The rating engine under xUnit and NSubstitute. -->

## Learning objectives

By the end of this chapter you can:

- Write a four-phase `[Fact]` test, Arrange, Act, Assert, against a class whose
  behavior depends on asynchronous collaborators.
- Parametrize a test with `[Theory]` and `[InlineData]`, and explain why a
  `decimal` test case has to arrive as a `double` literal rather than a
  `decimal` literal in the attribute itself.
- Build a `TheoryData<T1, T2>`-typed `[MemberData]` source and say why a typed
  source satisfies analyzer rules a bare `IEnumerable<object[]>` trips.
- Create an NSubstitute double with `Substitute.For<T>()`, stub a call with
  `.Returns(...)`, including per-argument stubs, and state what an unstubbed
  call on that same double returns.
- Verify interactions on an async substitute with `await ...Received(1)...`
  and `DidNotReceive()`, narrow those checks with `Arg.Any<T>()` and
  `Arg.Is<T>(predicate)`, and thread a cancellation token through every call
  the way the xUnit1051 analyzer requires.

## Why this matters

Every chapter after this one hands a class to a test and trusts the result.
That trust has to come from somewhere, and it comes from the suite in this
chapter, not from hope. A `ShipmentRatingEngine` that quotes a consignment by
calling out to a carrier's rate API, applying a jurisdiction's tax rules, and
writing a compliance record has three real-world dependencies that a test
cannot afford to touch: a network call to a carrier that may be down, rate
limited, or simply slow; a tax policy that might depend on data a test
environment does not have loaded; and an audit log that a test run should
never actually write to. None of those three concerns is what the test is
checking. The test is checking one thing, that `ShipmentRatingEngine` wires a
base rate through a tax policy and into an audit record correctly, and
everything else needs to get out of the way without weakening that check.

xUnit and NSubstitute solve two different halves of that problem. xUnit
supplies the structure a test runs inside: a `[Fact]` for a single invariant,
a `[Theory]` for the same invariant checked against several inputs, and a
runner that reports pass or fail per case with no ceremony around starting or
tearing down a test host. NSubstitute supplies the collaborators: a
`Substitute.For<ICarrierRates>()` stands in for the carrier integration,
answering instantly with whatever value the test tells it to return, and a
test can then ask that same substitute whether `ShipmentRatingEngine` called
it, with what arguments, and how many times. Neither tool needs a container,
a running service, or a shared fixture that one test's failure leaves dirty
for the next. A full run of this chapter's sixteen tests finishes in under a
second, on a laptop, with no setup beyond `dotnet test`, and that speed is not
a convenience, it is the property that makes running the suite on every
keystroke of a refactor a realistic habit rather than a once-a-day chore.
Every pattern example from here forward that claims "verified by a test"
means a test built on exactly this foundation: xUnit for the four-phase shape
and the parametrization, NSubstitute for a trustworthy, instant stand-in for
whatever the pattern under test talks to.

{% include excalidraw.html name="10-testing-with-xunit-and-nsubstitute" caption="Figure 10.1: The four-phase test around ShipmentRatingEngine. Arrange creates NSubstitute doubles for its three collaborators and stubs their return values, Act calls QuoteAsync once, and Assert checks both the returned quote and the Received interactions on each double." %}

## The four-phase test: Arrange, Act, Assert

`ShipmentRatingEngine` is the library this whole chapter tests, and it is
small by design: three constructor-injected collaborators, `ICarrierRates`,
`ITaxPolicy`, and `IAuditSink`, combined by one method.

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

`FactTests.Quotes_the_base_rate_with_tax_applied` is the canonical shape every
other test in the project follows, laid out with its three phases as their
own commented blocks rather than left for a reader to infer from spacing:

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

Arrange builds every collaborator the method under test needs and tells each
substitute what to return, including the audit sink, which gets no stub at
all here because `QuoteAsync` never reads a return value from it; it only
needs `auditSink` to exist and accept the call. Act is exactly one line,
calling `QuoteAsync` and awaiting its result. Assert checks exactly one
outcome, that the returned quote is the taxed amount, `110.00`, not the raw
base rate the carrier substitute returned. The [.NET unit-testing
best-practices guidance on
structure](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
names this same shape as "Arrange, Act, Assert" and recommends keeping each
phase visibly separate so "a reader can clearly separate what is being
tested from the Arrange and Assert tasks," which is exactly what the comments
in `FactTests` make explicit rather than leaving to inference. The same
guidance recommends avoiding multiple Act phases in one test, because a test
with two independent actions and two independent assertions makes a failure
ambiguous about which action actually broke; every test in this project's
suite calls `QuoteAsync`, or a single substitute member, exactly once.

One detail in the Arrange phase matters beyond this one test: the call
`engine.QuoteAsync(consignment, TestContext.Current.CancellationToken)` passes
a real cancellation token rather than leaving the parameter to a default or an
ad hoc `CancellationToken.None`. `TestContext.Current` is xUnit v3's
per-test execution context, and its `CancellationToken` property is already
wired to the test's own cancellation, including a test timeout if one is
configured, so passing it through a token-accepting call costs nothing and
lets the test framework tear down cleanly if the test runs too long. Every
test in this project threads a token this way, and the pattern repeats often
enough that it becomes invisible by the fourth or fifth test, which is
exactly the point: cancellation is not an afterthought bolted onto a test
after it already fails a review, it is part of the Arrange-Act shape from the
first line.

## [Theory] and [InlineData]: parametrizing with literal cases

A `[Fact]` proves one scenario. `ShipmentRatingEngine` needs the same
assertion checked against several destinations and weights, and rewriting the
whole Arrange-Act-Assert block for each one would multiply the test file
without adding any new behavior under test. `[Theory]` runs one test method
once per row of supplied data, and `[InlineData]` is the simplest way to
supply that data, literal values written directly on the attribute:

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

Three cases, one method body, three separately reported results: the xUnit v3
[getting-started
guide](https://xunit.net/docs/getting-started/v3/getting-started) describes
exactly this shape, "theories are tests which are only true for a particular
set of data," and the test runner reports each `[InlineData]` row as its own
named result rather than collapsing three cases into one pass or fail.

The literal `100.0`, `250.0`, and `900.0` in the attribute are not
`decimal` values, even though the method parameter they bind to is
`decimal kilograms`. C# requires every argument to an attribute to be a
compile-time constant of a type the attribute system supports directly, and
`decimal` is not one of those types; a `decimal` value is not a primitive the
runtime can bake into metadata the way it can an `int`, a `string`, or a
`double`. Writing `[InlineData("Rotterdam", 100.0m)]` does not compile. The
workaround here is a `double` literal, `100.0`, which the attribute accepts,
and an implicit conversion from `double` to `decimal` at the point the
parameter binds, which the compiler performs automatically because every
`double` value this chapter's test cases use converts to `decimal` without
loss. This is not a quirk specific to `ShipmentRatingEngine`'s tests; any
`[Theory]` taking a `decimal` parameter and driven by `[InlineData]` hits the
identical constraint, and the fix is always the same `double`-literal
substitution, never a cast or a helper method, because the attribute
constructor call has to resolve entirely at compile time before any method
body, helper or otherwise, could run.

## Typed TheoryData: member data that satisfies the analyzers

`[InlineData]` covers a handful of short, literal cases well, but it runs out
of room once a case needs a name worth reading rather than an unlabeled
position in an argument list, or once the values involved are more than a
couple of scalars. `[MemberData]` points a `[Theory]` at a static member that
produces the data instead, and the member itself can be given a name that
describes what each row represents:

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

`WeightTierBaseRates` sidesteps the `[InlineData]` attribute-argument
constraint entirely, because a static method body is ordinary C# executed at
test-discovery time, not an attribute constructor resolved at compile time,
so a `decimal` literal is simply a `decimal` literal here, no `double`
substitution needed. More important than the convenience is the declared
return type, `TheoryData<decimal, decimal>`, rather than the older, more
general `IEnumerable<object[]>` shape a `[MemberData]` source could
technically also return. The xUnit analyzers that ship with the project and
run under this book's warnings-as-errors build flag two different problems
with the untyped shape. [xUnit1019, "MemberData must reference a member
providing a valid data
type"](https://xunit.net/xunit.analyzers/rules/xUnit1019), catches a member
whose return type the runner cannot actually enumerate into theory rows at
all, a mismatch that would otherwise surface only when the suite runs rather
than when it builds. [xUnit1042, "The member referenced by the MemberData
attribute returns untyped data
rows"](https://xunit.net/xunit.analyzers/rules/xUnit1042), catches the
specific case of a `MemberData` source built on `IEnumerable<object[]>`,
recommending `TheoryData<>` instead precisely because the untyped shape
defeats compile-time checking of each row against the test method's actual
parameter types: a row with the wrong number of elements, or elements in the
wrong order, compiles cleanly against `object[]` and only fails at runtime
when the test tries to invoke the method with mismatched arguments.
`TheoryData<decimal, decimal>` makes that mistake a compile error instead,
because the generic type parameters pin the shape of every row added to the
collection initializer to exactly two `decimal` values, in that order,
checked the moment the file compiles.

## NSubstitute basics: Substitute.For and .Returns

Every test up to this point has already used `Substitute.For<T>()` without
pausing to explain it, because the idiom is short enough to read correctly on
first sight: call `Substitute.For<ITaxPolicy>()` and get back an object that
implements `ITaxPolicy`, answers every call with a default value until told
otherwise, and remembers every call it receives so a test can ask about it
later. The [NSubstitute getting-started
guide](https://nsubstitute.github.io/help/getting-started/) frames the whole
library around that one factory method: no base class to inherit, no mock
repository to construct first, no `.Object` property standing between the
substitute and the interface it implements. `SubstituteBasicsTests` proves
three distinct behaviors of that one factory method and its paired
`.Returns(...)` extension, working entirely against the synchronous
`ITaxPolicy.ApplyTax(Money)` member so the async machinery covered later in
this chapter never muddies what `.Returns` itself does.

The first behavior is the one every other test already relies on: a stubbed
call answers with exactly the value the test told it to answer with. The
second is less obvious and more useful once a class's logic branches on its
collaborator's input:

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

Each `.Returns(...)` call is scoped to the exact argument the preceding call
expression specified, `new Money(100.00m)` in the first line and
`new Money(200.00m)` in the second, so the same substitute answers two
different inputs with two different, independently configured outputs. The
[NSubstitute return-value
documentation](https://nsubstitute.github.io/help/set-return-value/) explains
the mechanism behind the syntax directly: the call `taxPolicy.ApplyTax(...)`
first runs against the substitute, which records the arguments and returns a
placeholder, and the `.Returns(...)` extension method that follows then
attaches the stub to that specific recorded call. Nothing about the
substitute needs a conditional or a lookup table written by hand; recording
two separately stubbed calls is enough for NSubstitute to match future calls
against whichever stub's arguments match.

The third behavior answers a question a reader of the first two tests might
reasonably ask: what happens when a test calls a member of the substitute
that nobody stubbed.

```csharp
[Fact]
public void An_unstubbed_call_on_a_substitute_returns_the_types_default_value()
{
    var taxPolicy = Substitute.For<ITaxPolicy>();

    var result = taxPolicy.ApplyTax(new Money(50.00m));

    Assert.Equal(default, result);
}
```

`Money` is a `readonly record struct`, and `default(Money)` is a
zero-initialized value, `Amount == 0m`. An unstubbed substitute never throws
for lacking a stub the way a strict mock in some other frameworks might; it
quietly returns the member's type default and lets the test continue, which
means a test that forgets to stub a collaborator the code under test actually
calls fails on its assertion, with a `0m` result that is easy to trace back
to the missing stub, rather than failing with an unrelated exception from
deep inside the substitute's own machinery. Knowing this behavior in advance
turns a confusing `Assert.Equal` failure into an immediately recognizable
signature: a zero, an empty string, or a `null` coming out of a test that
clearly expected something else almost always means a stub that was never
set up, not a bug in the substitute.

## Verifying interactions: Received, DidNotReceive, and Arg matchers

Stubbing a return value answers "what does the collaborator hand back."
`ReceivedTests` answers the other half of the same question a
behavior-verifying test needs answered: "did the class under test actually
call its collaborator, with what, and how many times." `IAuditSink` is the
right collaborator to check this against, because `ShipmentRatingEngine`
never reads a return value from it; the entire contract that matters is that
`RecordAsync` gets called, once, with the right consignment ID and the right
quoted amount.

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

`auditSink.Received(1)` reads almost like English: assert that the substitute
received exactly one call matching the arguments on the member invocation
that follows it. The [NSubstitute received-calls
documentation](https://nsubstitute.github.io/help/received-calls/) describes
`Received()` and its argument-taking overload as checking "that a specific
call has been received by a substitute," with the count argument narrowing
the check from "at least once" to an exact number; a `ShipmentRatingEngine`
that accidentally recorded the same quote twice, perhaps from a retry loop
added without thinking through its interaction with the audit requirement,
would fail this assertion even though the quote itself still came back
correct, because correctness here includes not auditing the same shipment
twice.

`Arg.Any<CancellationToken>()` on the same line is an argument matcher, not a
literal value: it tells the verification to accept any token at that
position rather than requiring the exact `CancellationToken` instance the
test happened to pass in. The [NSubstitute argument-matchers
documentation](https://nsubstitute.github.io/help/argument-matchers/)
describes `Arg.Any<T>()` as the way to "ignore the actual argument" at a
position a test does not care about, which matters here because the token
`TestContext.Current.CancellationToken` supplies is not something the test
wants to hardcode into its own assertion; what the test cares about is the
consignment ID and the quoted amount, not which exact token object flowed
through. `DidNotReceive()` is the negative counterpart, proving an
interaction never happened rather than happened a specific number of times,
and it pairs naturally with a predicate matcher rather than a fixed value
when the thing being ruled out is a category of call rather than one exact
one:

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

`Arg.Is<Money>(quoted => quoted != new Money(110.00m))` is the predicate form
of the same matcher family: instead of ignoring the argument entirely, it
accepts a lambda that decides, call by call, whether a given argument
matches. The assertion reads as "the audit sink never recorded any amount
other than the correct quote," a check `Arg.Any<Money>()` alone could never
express, since that matcher would match the correct quote too and the
assertion would prove nothing. `Records_against_a_consignment_id_that_matches_a_predicate`
uses the same predicate matcher on the other parameter position, confirming
the consignment ID recorded starts with the expected prefix without pinning
the assertion to one exact literal string. Between `Arg.Any<T>()` for
"this position does not matter to this assertion" and `Arg.Is<T>(predicate)`
for "this position matters, but only this property of it," a verification
can express exactly the claim a test needs to make and nothing broader.

{% include excalidraw.html name="10-nsubstitute-stub-vs-verify" caption="Figure 10.2: A substitute's two jobs, stubbing a return value on Arrange and verifying a call on Assert, with Arg matchers working on both sides." %}

## Async substitutes and cancellation tokens

`ICarrierRates.GetBaseRateAsync` and `IAuditSink.RecordAsync` are both
`Task`-returning members, and every example so far has already stubbed and
verified them without any special async-specific syntax, because NSubstitute
treats a `Task<T>`-returning member's `.Returns(...)` call the same way it
treats a synchronous one: supply the value the caller should eventually
observe, and the substitute handles wrapping it appropriately.

```csharp
[Fact]
public async Task An_async_substitute_method_returns_a_completed_task_wrapping_the_stubbed_value()
{
    var carrierRates = Substitute.For<ICarrierRates>();
    carrierRates.GetBaseRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
        .Returns(new Money(76.00m));

    var baseRate = await carrierRates.GetBaseRateAsync(
        "Rotterdam", 820m, TestContext.Current.CancellationToken);

    Assert.Equal(new Money(76.00m), baseRate);
}
```

`.Returns(new Money(76.00m))` takes the plain `Money` value, not a
`Task<Money>`, and the call site awaits `GetBaseRateAsync` exactly as it
would await the real carrier integration. `GetBaseRateAsync`'s declared
return type is `Task<Money>`, so the substitute wraps the stubbed value in an
already-completed task behind the scenes; the test never constructs that task
itself, and nothing about the calling code has to know or care whether the
value arrived synchronously or asynchronously, which is exactly the
behavior a real `await` over a network call would also exhibit from the
caller's perspective once the call actually finishes.

Verifying a call that was awaited needs one detail the earlier synchronous
`ApplyTax` verifications did not: `Received(...)` itself returns an object
whose member invocation needs to be awaited, because that member invocation
is just as asynchronous as the original call it is checking.

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

Dropping the `await` in front of `carrierRates.Received(1)...` still compiles,
because the expression's static type is itself a `Task`, but it stops
verifying anything meaningful: the verification task starts, the test method
moves on without observing its result, and a mismatch that should fail the
test can slip past silently or surface as an unobserved-task warning instead
of a clear assertion failure. The fix is mechanical once named: every
`Received(...)` or `DidNotReceive()` check against a member that returns a
`Task` needs its own `await`, exactly like the original call it is
re-checking.

The last piece ties cancellation all the way through the call chain rather
than treating it as an afterthought at one layer.

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

This test passes a caller-owned `CancellationTokenSource`'s token into
`QuoteAsync` instead of `TestContext.Current.CancellationToken`, specifically
so the two `Received(1)` checks afterward can verify that exact token
instance arrived unchanged at both `GetBaseRateAsync` and `RecordAsync`.
`ShipmentRatingEngine.QuoteAsync` never constructs its own token, substitutes
`CancellationToken.None` partway through, or drops the token before the
second collaborator call; the same token flows end to end. That matters
operationally, not just as a style preference: a consumer cancelling a batch
of in-flight quotes expects cancellation to reach every downstream call a
quote triggers, not just the first one, and a `ShipmentRatingEngine` that
dropped the token before the audit call would leave an audit write running
after the caller asked everything to stop. The [xUnit1051 analyzer
rule](https://xunit.net/xunit.analyzers/rules/xUnit1051), "calls to methods
which accept CancellationToken should use TestContext.Current.CancellationToken,"
enforces the test-side half of this discipline directly: under this book's
warnings-as-errors build, any token-accepting call in a test body that
doesn't pass a real token, neither `TestContext.Current.CancellationToken`
nor an explicit one like `cancellationSource.Token` here, fails the build
before the test ever runs, which is exactly the forcing function that keeps
every test in this project threading a token rather than letting one slip
through on `CancellationToken.None` by habit.

## NSubstitute versus Moq

Moq is the other mocking library most C# developers have already used, and
nothing about this book's choice of NSubstitute argues that a team already
standardized on Moq should migrate a working suite. The syntax differs in a
way that matters here. Moq wraps the substituted type behind a `Mock<T>`
instance, and the test calls through a `.Setup(...)` expression and an
`.Object` property to get at the actual interface instance the code under
test receives: `var mock = new Mock<ITaxPolicy>(); mock.Setup(t =>
t.ApplyTax(It.IsAny<Money>())).Returns(...); var taxPolicy = mock.Object;`.
NSubstitute collapses that indirection: `Substitute.For<ITaxPolicy>()` returns
the `ITaxPolicy` instance directly, and `.Returns(...)` attaches to the call
expression itself rather than to a `.Setup(...)` wrapper, which is the
pattern every test in this chapter already leans on.

The other reason a new project reaches for NSubstitute over Moq is not
syntax. Moq 4.20 shipped a component called SponsorLink that collected a
hashed form of the developer's email address at build time, bundled as a
closed-source binary with limited notice to consumers of the package, and the
resulting community pushback was substantial enough that the maintainers
reverted the default behavior; the underlying mechanism has remained
available as an opt-in rather than removed outright. NSubstitute has no
comparable telemetry dependency to evaluate, which removes one more thing a
team adopting a mocking library for a new project has to research before
trusting it in a build pipeline. None of this is an argument for ripping Moq
out of an existing, working test suite; a migration like that carries its own
risk and buys a team nothing a passing test suite wasn't already providing.
It is a reason a new project, with no existing investment either way, can
reach for NSubstitute by default.

> **AI sidebar.** Ask an AI pairing partner for "a mocking example in C#"
> without naming a library and a generated answer defaults to Moq more often
> than not, because Moq's `.Setup(...)`/`.Object` shape appears far more often
> in the training data an LLM draws from than NSubstitute's shorter syntax
> does. That default is harmless by itself, Moq still works, but it quietly
> reintroduces the `.Setup(...)`/`.Object` indirection this book's examples
> avoid by design, and a generated suite built that way will not match the
> idiom this book's other tests use if it gets pasted into this
> project's test folder. A generated test is also prone to a subtler mistake
> this chapter named directly: stubbing an async member with
> `.Returns(Task.FromResult(value))` where a plain `.Returns(value)` would do,
> carried over from an older NSubstitute version or from Moq's own async
> conventions, which still compiles and still passes but adds a manual
> `Task.FromResult` wrapper the current library already performs for a
> `Task<T>`-returning member. Check a generated test against the project's
> own examples before trusting it: does it use `Substitute.For<T>()` rather
> than a `Mock<T>` and `.Object`, does it thread a real cancellation token
> instead of `CancellationToken.None`, and does it await its own
> `Received(...)` checks the way `AsyncSubstituteTests` does here.

## Build, run, observe

Every command below runs with the local .NET 10 SDK; nothing in this loop
needs a container. The example is standalone-testable from its own
directory:

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 16
  failed: 0
  succeeded: 16
  skipped: 0
  duration: 914ms
```

The sixteen cases split across five files, and each one maps back to a
specific idiom this chapter named. `FactTests` supplies the one canonical
four-phase `[Fact]`. `TheoryTests` supplies six: three `[InlineData]` rows
proving the destination and weight reach the carrier lookup unchanged, and
three `TheoryData<decimal, decimal>` rows proving the carrier's quoted base
rate for each named weight tier flows through to the final quote.
`SubstituteBasicsTests` supplies three, covering a single stubbed return, two
independently stubbed arguments on the same member, and the type-default
fallback for a call nobody stubbed. `ReceivedTests` supplies three,
verifying an exact audit count, a predicate ruling out every amount but the
correct one, and a predicate matching a consignment ID prefix.
`AsyncSubstituteTests` supplies the remaining three, proving the completed-
task wrapping behavior, the `await` requirement on an async `Received(...)`
check, and the cancellation token's end-to-end path through both downstream
calls. Every test in the file maps to a sentence in this chapter; none of
them exists only to pad a count.

## For deeper coverage

- The official xUnit v3 documentation: [Getting Started with
  xUnit.net](https://xunit.net/docs/getting-started/v3/getting-started) for
  `[Fact]` and `[Theory]`, and the analyzer rule pages
  [xUnit1019](https://xunit.net/xunit.analyzers/rules/xUnit1019),
  [xUnit1042](https://xunit.net/xunit.analyzers/rules/xUnit1042), and
  [xUnit1051](https://xunit.net/xunit.analyzers/rules/xUnit1051) behind this
  chapter's typed-`TheoryData` and cancellation-token guidance.
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
  command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
  this chapter's build step runs.
- Gerard Meszaros, [*xUnit Test Patterns: Refactoring Test
  Code*](https://www.informit.com/store/xunit-test-patterns-refactoring-test-code-9780132800051)
  (Addison-Wesley): the source of the four-phase test and the test-double
  taxonomy, stub versus mock, this chapter builds on.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
