---
title: "xUnit reference"
order: 48
layout: tutorial
sectionid: docs
part: appendices
appendix: "B"
description: "A reference for the xUnit v3 test framework the examples are written against."
---

## Learning objectives

By the end of this appendix you can:

- Choose between `[Fact]` and `[Theory]`, and pick `[InlineData]`,
  `[MemberData]` or a typed `TheoryData<...>` source for a theory's cases.
- Pick the `Assert` member for each kind of claim: equality, truth, reference
  identity, collection size, type and thrown exceptions.
- Pass `TestContext.Current.CancellationToken` into token-accepting calls,
  and say what xUnit1051 warns about.
- Run tests with `dotnet test --project <proj>` from the repository root and
  with a bare `dotnet test` from one example's directory.
- Apply the underscore naming convention and scope the CA1707 suppression to
  test projects.

## Why this matters

Every test project uses xUnit v3, not the v2 line most existing .NET
codebases still run. The `xunit.v3` package rewrites the runner architecture:
it drops the VSTest adapter path and runs on Microsoft.Testing.Platform, a
leaner host that starts a test assembly as its own console executable. Most
test code looks the same (a `[Fact]` is still a `[Fact]`), but three things
change for a reader coming from v2: how a test gets its cancellation token,
how `dotnet test` is invoked from a script, and what a theory's data source
may look like.

## Facts, theories, and data sources

| Attribute | Use it for |
| --- | --- |
| `[Fact]` | One case with no parameters |
| `[Theory]` + `[InlineData]` | A handful of literal cases |
| `[Theory]` + `[MemberData]` returning `TheoryData<...>` | Named or computed rows, typed at compile time |

A `[Fact]` follows the Arrange, Act, Assert shape, with substitute setup in
Arrange and each phase commented when the body is long:

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

A `[Theory]` runs the body once per data row. `[InlineData]` supplies literal
rows:

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

`[MemberData]` pulls rows from a method. Type its return as
`TheoryData<...>`, not `IEnumerable<object[]>`:

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

`TheoryData<T1, T2, ...>` keeps every row strongly typed, so a mismatched
argument fails at compile time instead of as a runtime cast inside the
runner. It also satisfies the analyzer rule that flags an untyped `object[]`
source, which `EnforceCodeStyleInBuild` turns into a build failure.

## Assert: the common members

| Claim | Members |
| --- | --- |
| Value equality | `Assert.Equal`, `Assert.NotEqual` |
| Reference identity | `Assert.Same`, `Assert.NotSame` |
| Truth | `Assert.True`, `Assert.False` |
| Presence | `Assert.Null`, `Assert.NotNull` |
| Collections | `Assert.Single` (returns the element), `Assert.Empty`, `Assert.NotEmpty`, `Assert.Contains`, `Assert.DoesNotContain` |
| Exact runtime type | `Assert.IsType<T>` |
| Exceptions | `Assert.Throws<T>`, `Assert.ThrowsAsync<T>`, `Assert.ThrowsAnyAsync<T>` |
| Less common | `Assert.Matches` (regex), `Assert.InRange` (numeric bound), `Assert.Collection` (element by element) |

`Assert.Equal` compares records such as `Money` and `Consignment` by value.
`Assert.Same` checks reference identity, which is the claim to make when a
dependency injection container must return one registered instance through
several interfaces:

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

With nullable reference types on, `Assert.NotNull` checks at run time the
promise a non-nullable return type makes at compile time.

`Assert.IsType<T>` checks that the runtime type is exactly `T`, the claim for
a factory whose job is choosing the concrete type:

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

`Assert.Throws<T>` runs a synchronous delegate, requires exactly `T`, and
returns the exception so you can check its message or properties.
`Assert.ThrowsAsync<T>` awaits an async delegate. `Assert.ThrowsAnyAsync<T>`
also accepts a subtype, which helps when a chain of awaited calls can surface
the failure as a different derived type depending on the layer:

```csharp
[Fact]
public void Constructor_rejects_a_non_positive_weight()
{
    var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Weight(0m));

    Assert.Equal("Kilograms", exception.ParamName);
}
```

## TestContext.Current and the cancellation token

xUnit v3 exposes the running test's context through the static
`TestContext.Current`, with no constructor parameter or base class. Its
`CancellationToken` is tied to the running test's lifetime and is canceled when the
runner stops a run:

```csharp
var baseRate = await carrierRates.GetBaseRateAsync(
    "Rotterdam", 820m, TestContext.Current.CancellationToken);
```

Pass that token to every async, token-accepting call instead of letting the
parameter default to `CancellationToken.None`. The analyzer rule xUnit1051
flags a call that doesn't forward one: a test on `None` can't be canceled,
so a hung collaborator blocks the whole run instead of being torn down
with it. When the behavior being checked is cancellation itself, pass the
token of your own `CancellationTokenSource` instead.

## Running tests: Microsoft.Testing.Platform and dotnet test

The root `global.json` selects Microsoft.Testing.Platform for every test
project:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Under this runner, `dotnet test` from the repository root needs each project
named with `--project`; a bare positional path is rejected:

```sh
dotnet test --project examples/10-testing-with-xunit-and-nsubstitute/RatingEngine.Tests/RatingEngine.Tests.csproj
```

`scripts/verify.sh` runs that command for every test project under
`examples/`. From one example's directory a bare `dotnet test` works, because
there's only one test project to find:

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

Both run the same `xunit.v3` package on the same host.

## Naming convention and the CA1707 exception

Test methods read as a sentence describing a behavior, with underscores
instead of camel case, as in the listings above. A failing test report then
states what broke without opening the method.

Roslyn's CA1707 flags underscores in identifiers. Each test project's
`.csproj` suppresses it for that project only:

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

The library under test keeps CA1707 on, so the exception never reaches
production types and members.

## Package pins

Each test project pins the same four packages in its own `.csproj`:

| Package | Version |
|---|---|
| `xunit.v3` | 4.0.2 |
| `xunit.runner.visualstudio` | 4.0.1 |
| `Microsoft.NET.Test.Sdk` | 18.11.0 |
| `NSubstitute` | 6.2.0 |

There's no central package management file. Each example stays
standalone and copyable, at the cost of updating a version in every test
project when the pins change.

## For deeper coverage

- [Getting started with xUnit.net v3](https://xunit.net/docs/getting-started/v3/getting-started)
  is the framework's own setup and authoring guide for the pinned version.
- [xunit.net](https://xunit.net/) is the project's home page, with links to
  the full `Assert` reference and release notes for the `xunit.v3` line.
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  covers the host xUnit v3 runs on, including the
  `dotnet test --project` requirement the scripts work with.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
