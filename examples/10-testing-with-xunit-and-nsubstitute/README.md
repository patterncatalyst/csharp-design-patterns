# 10-testing-with-xunit-and-nsubstitute

`ShipmentRatingEngine`, a minimal shipping-rating collaborator with three
constructor-injected dependencies: `ICarrierRates`, `ITaxPolicy`, and
`IAuditSink`. The library is deliberately thin. The teaching artifact in this
example is the **test project**: every xUnit v3 and NSubstitute idiom the rest
of this book relies on appears here as a real, green test.

**The library.** `ShipmentRatingEngine.QuoteAsync(Consignment, CancellationToken)`
looks up a carrier's base rate for a consignment's destination and weight,
applies tax, records the result to an audit sink, and returns the quote.
`Money` and `Consignment` are minimal value types local to this example, built
fresh rather than shared with an earlier chapter.

## What the tests prove

`FactTests` is the canonical four-phase `[Fact]` test: Arrange (including
substitute setup), Act, Assert, laid out as its own commented block.
`TheoryTests` parametrizes with both `[InlineData]` and a
`TheoryData<decimal, decimal>`-typed `[MemberData]` source, naming each weight
tier instead of indexing into positional arguments. `SubstituteBasicsTests`
builds a `Substitute.For<ITaxPolicy>()` and stubs its synchronous return
value, including the type-default behavior of an unstubbed call.
`AsyncSubstituteTests` stubs an async collaborator with `.Returns(someMoney)`
(NSubstitute wraps the value in a completed task), awaits `Received(1)` to
verify an awaited call, and threads `TestContext.Current.CancellationToken` or
a caller-owned token through every call. `ReceivedTests` verifies interactions
against `IAuditSink` with `Received(1)`, `DidNotReceive()`, `Arg.Any<T>()`,
and `Arg.Is<T>(predicate)`.

## Why NSubstitute, not Moq

This book standardizes on NSubstitute for test doubles. Moq remains a
reasonable choice for a team already standardized on it, and nothing here
argues for migrating a working Moq suite. A new project still has a reason to
prefer NSubstitute: Moq 4.20 shipped a proprietary, closed-source telemetry
component (SponsorLink) that collected a hashed email address at build time
without clear notice, which drew enough community objection that the
maintainers reverted it; the underlying mechanism remained available as an
opt-in. NSubstitute carries no comparable dependency, no telemetry, and no
`.Setup(...)`/`.Object` indirection: a substitute is just
`Substitute.For<T>()`.

## Build and test

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
