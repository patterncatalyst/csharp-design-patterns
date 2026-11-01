---
title: "xUnit reference"
order: 48
layout: tutorial
sectionid: docs
part: appendices
appendix: "B"
description: "A reference for the xUnit test framework this book writes its suites against."
---

## Learning objectives

By the end of this appendix you can:

- Choose between `[Fact]` and `[Theory]`, and pick `[InlineData]`,
  `[MemberData]`, or a typed `TheoryData<...>` source for a parametrized
  test's cases.
- Name the `Assert` members this book's suites rely on most and the shape of
  test each one fits: equality, truth, reference identity, collection size,
  and thrown exceptions.
- Explain why a test passes `TestContext.Current.CancellationToken` into a
  token-accepting call, and what xUnit1051 is warning you about when you
  don't.
- Run a suite with `dotnet test --project <proj>` at the repository level and
  with a bare `dotnet test` from a single example's own directory, and state
  which runner is doing the work in each case.
- Apply the house test-method naming convention and explain why its test
  projects suppress CA1707 only for themselves.

## Why this matters

Every test in this book is written against xUnit v3, not the v2 line most
existing .NET codebases still run. The package pinned in every test project,
`xunit.v3`, is a from-scratch rewrite of the framework's runner architecture:
it drops the legacy VSTest adapter path entirely and runs on
Microsoft.Testing.Platform, a leaner host that starts a test assembly as its
own console executable. That change is invisible in most of the test code
you write, a `[Fact]` still looks like a `[Fact]`, but it reaches into a
handful of places a reader coming from xUnit v2 or another framework will
notice: how a test gets its cancellation token, how `dotnet test` is invoked
from a script, and what a theory's data source is allowed to look like. This
appendix collects the v3 surface this book's suites use, so a
chapter introducing a pattern can show a test without re-explaining the
framework underneath it, and you have one place to check whether an xUnit
idiom you remember from elsewhere still applies.

## Facts, theories, and data sources

A `[Fact]` is a single test case with no parameters. It is the default shape
for a test that checks one behavior with one set of inputs:

```csharp
public class FactTests
{
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
}
```

This is the four-phase shape this book's suites follow throughout: Arrange
(including any substitute setup), Act, Assert, with each phase commented when
a test's body is long enough to benefit from the labels.

A `[Theory]` runs the same test body once per data row. `[InlineData]` is the
right tool for a handful of literal cases:

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

For a case that calls for a name rather than an index into positional
literals, `[MemberData]` pulls its rows from a method, and this book always
types that method's return as `TheoryData<...>` rather than a bare
`IEnumerable<object[]>`:

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
    // ...
}
```

A `TheoryData<T1, T2, ...>` source keeps every row strongly typed, catches a
mismatched argument at compile time instead of a runtime cast failure inside
the test runner, and satisfies the analyzer rule that flags an untyped
`object[]` theory source as a style violation under this book's
`EnforceCodeStyleInBuild` setting.

## Assert: the members this book's suites lean on

xUnit v3's `Assert` class carries the same broad surface the framework has
always had; the subset below is what this book's suites reach for, grouped by
the shape of the claim they check.

Equality and comparison: `Assert.Equal(expected, actual)` is the workhorse
for value types and records, comparing by value the way a `Money` or
`Consignment` record is designed to compare. `Assert.NotEqual` is its
negation. `Assert.Same(expected, actual)` and `Assert.NotSame` check
reference identity, which matters when a test is proving that a dependency
injection container handed back the exact same registered instance rather
than two separately constructed ones that happen to compare equal:

```csharp
[Fact]
public async Task Narrow_interfaces_resolve_to_the_same_registered_gateway_instance()
{
    var services = new ServiceCollection();
    services.AddCarrierGateway();
    await using var provider = services.BuildServiceProvider();

    var gateway = provider.GetRequiredService<ICarrierGateway>();
    var rates = provider.GetRequiredService<ICarrierRates>();
    var tracking = provider.GetRequiredService<ICarrierTracking>();

    Assert.Same(gateway, rates);
    Assert.Same(gateway, tracking);
}
```

Truth and nullability: `Assert.True` and `Assert.False` check a boolean
condition directly, usually a predicate that would otherwise need its own
named assertion. `Assert.Null` and `Assert.NotNull` check presence or
absence, which under this book's nullable reference types setting is as much
a runtime behavior check as it is a type-level claim: a method that promises
a non-null return is asked to honor that promise at run time, not merely
compile as if it does.

Collections: `Assert.Single(collection)` checks that a collection holds
exactly one element and hands that element back for further assertions,
`Assert.Empty` and `Assert.NotEmpty` check the zero and non-zero cases, and
`Assert.Contains` / `Assert.DoesNotContain` check membership, either by value
or with a predicate.

Types: `Assert.IsType<T>(value)` checks that a value's runtime type is
exactly `T`, which this book reaches for when a factory's whole job is
choosing which concrete type to hand back:

```csharp
[Fact]
public void Resolves_the_fastline_family_by_type()
{
    var registry = BuildRegistryWithoutAirco();

    var factory = registry.FactoryFor<Fastline>();

    Assert.IsType<FastlineGateway>(factory.CreateGateway());
    Assert.IsType<FastlineRequestBuilder>(factory.CreateRequestBuilder());
    Assert.IsType<FastlineResponseParser>(factory.CreateParser());
}
```

Exceptions: `Assert.Throws<TException>(() => ...)` runs a synchronous
delegate and asserts it throws exactly `TException`, handing back the caught
exception so a test can assert on its message or properties too. The async
counterpart, `Assert.ThrowsAsync<TException>`, awaits an async delegate and
asserts the same thing; `Assert.ThrowsAnyAsync<TException>` relaxes the exact
type to "this type or a subtype," useful when a chain of awaited calls could
surface the failure wrapped differently depending on which layer observes it
first.

```csharp
[Fact]
public void Constructor_rejects_a_non_positive_weight()
{
    var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(0m));

    Assert.Equal("Kilograms", exception.ParamName);
}
```

None of this is the complete `Assert` surface. It is the subset that covers
nearly every test in this book, and a chapter that needs one more member,
`Assert.Matches` for a regular expression, `Assert.InRange` for a numeric
bound, `Assert.Collection` for element-by-element checks over a sequence,
introduces it in place with a short comment rather than assuming you already
know it.

## TestContext.Current and the token every call threads through

xUnit v3 gives every running test access to its context through a static
property, `TestContext.Current`, rather than requiring a constructor
parameter or a base class to opt in. The member this book's suites use
constantly is `TestContext.Current.CancellationToken`, a token tied to the
test's own lifetime, canceled if the test runner is asked to stop a run in
progress:

```csharp
var baseRate = await carrierRates.GetBaseRateAsync(
    "Rotterdam", 820m, TestContext.Current.CancellationToken);
```

Every async, token-accepting call in this book's test suites passes that
token (or a caller-owned `CancellationTokenSource.Token` when a test is
specifically exercising cancellation behavior) instead of leaving the
parameter to default to `CancellationToken.None` implicitly. The built-in
analyzer rule xUnit1051 flags a test that calls a cancellation-token-
accepting method without forwarding one, precisely because a test that
silently defaults to `None` can't be canceled if the run is stopped early,
and a hung or slow collaborator in that test blocks the whole suite instead
of being torn down with it. Passing `TestContext.Current.CancellationToken`
is the fix for essentially every instance of that warning in this book's
examples; a test with its own `CancellationTokenSource`, usually one proving
that a method respects cancellation, passes that source's token instead,
since that is the token under test.

## Running the suite: Microsoft.Testing.Platform and dotnet test

This book's test projects run on Microsoft.Testing.Platform rather than the
older VSTest-based `dotnet test` path, selected once for the whole repository
by the root `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

That selection has one concrete, visible effect on the command line: running
every example's tests from the repository root needs each project path
spelled out with `--project`, because this runner mode rejects a bare
positional path the older one accepted:

```sh
dotnet test --project examples/10-testing-with-xunit-and-nsubstitute/RatingEngine.Tests/RatingEngine.Tests.csproj
```

`scripts/verify.sh` runs exactly that command for every test project under
`examples/`. Working on a single example from its own directory is simpler,
because there's only one test project for the runner to find:

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

Both invocations exercise the same `xunit.v3` package and the same
Microsoft.Testing.Platform host; the `--project` flag only matters once a
script is driving more than one project from a directory above them.

## Naming convention and the CA1707 exception

Every test method in this book reads as a sentence describing a behavior,
written with underscores rather than camel case:
`Records_exactly_one_audit_entry_for_the_quoted_consignment`,
`Constructor_rejects_a_blank_destination`,
`Verifying_an_awaited_call_needs_an_await_in_front_of_received`. A name in
this shape reads directly as the assertion a failing test report makes,
without needing to open the method body to find out what broke.

Roslyn's CA1707 analyzer flags identifiers containing underscores, which
would otherwise fight this convention on every single test method. Rather
than disable the rule project-wide, each test project's `.csproj` suppresses
it only for itself:

```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
  <!-- CA1707 (no underscores in identifiers) fights the house xUnit naming
       convention (Method_does_thing); suppress it here only, not in the
       library under test. -->
  <NoWarn>$(NoWarn);CA1707</NoWarn>
</PropertyGroup>
```

The library project under test keeps CA1707 active, so the naming exception
never leaks past the one place it's meant for: a test's own method names, not
the production types and members the tests exercise.

## The test package pins

Every test project in this book pins the same four packages directly in its
own `.csproj`, surveyed and dated at the point the project was created rather
than assumed from memory:

| Package | Version |
|---|---|
| `xunit.v3` | 4.0.2 |
| `xunit.runner.visualstudio` | 4.0.1 |
| `Microsoft.NET.Test.Sdk` | 18.11.0 |
| `NSubstitute` | 6.2.0 |

There is no central package management file consolidating these across the
repository; each test project's `.csproj` carries its own
`<PackageReference>` entries at these versions. That keeps every example
standalone, copyable out of this repository with nothing left behind in a
shared file one or more directories up, at the cost of updating a version in
more than one place when this book re-surveys its pins.

## For deeper coverage

- [Getting started with xUnit.net v3](https://xunit.net/docs/getting-started/v3/getting-started)
  is the framework's own setup and authoring guide for the version this book
  pins.
- [xunit.net](https://xunit.net/) is the project's home page, with links to
  the full `Assert` reference and release notes for the `xunit.v3` line.
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  covers the test host xUnit v3 runs on in .NET 10, including the
  `dotnet test --project` requirement this book's scripts work with.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
