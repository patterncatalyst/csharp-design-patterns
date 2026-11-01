---
title: "xUnit test patterns"
order: 50
layout: tutorial
sectionid: docs
part: appendices
appendix: "D"
description: "The four-phase test and test-double taxonomy from Meszaros, applied to the book's xUnit suites."
---

## Learning objectives

- Name the four phases of a test, setup, exercise, verify, teardown, and map
  them onto the Arrange/Act/Assert shape this book's tests write.
- Define the five members of Meszaros's test-double taxonomy: dummy, stub,
  spy, mock, and fake.
- Tell which role a given NSubstitute substitute is playing in a specific
  test, since the same substitute can be a stub in one assertion and a mock
  in the next.
- Recognize this book's hand-written in-memory collaborators as fakes, and
  explain why a fake earns its own suite of tests separate from the code that
  consumes it.

## Why this matters

Every xUnit test in this book's examples has the same shape, and every
collaborator it isolates is one of a small number of kinds. Gerard Meszaros's
*xUnit Test Patterns* named both of those things a long time before this book
existed: the four-phase test, and the test-double taxonomy of dummy, stub,
spy, mock, and fake. Without that vocabulary, a reader is left calling
everything that is not the real collaborator "a mock," which collapses five
distinct design decisions into one word. A stub that hands back a
canned tax rate is doing something different from a mock that asserts an
audit entry was recorded exactly once, and a hand-written in-memory ledger
that accumulates real state across a test is doing something different again
from either. Naming the difference is not pedantry. It changes what a test
failure tells you: a failed stub assertion says "the collaborator's output
drove the wrong behavior," a failed mock assertion says "the system under
test didn't call its collaborator the way it was supposed to," and a failed
fake-backed assertion says "the state the system under test accumulated is
wrong." This appendix gives that vocabulary a fixed meaning and ties each
term to a real test already in `examples/`.

## The four-phase test

Meszaros describes every good test as four phases, whether or not the test's
author wrote them as four separate blocks:

- **Setup.** Build the system under test and its collaborators, and put them
  into the state the test needs before anything is exercised.
- **Exercise.** Call the one behavior the test is about.
- **Verify.** Check the outcome: a return value, a thrown exception, or an
  interaction with a collaborator.
- **Teardown.** Release anything setup acquired that would otherwise leak
  into the next test.

This book's tests write the first three phases as the familiar
Arrange/Act/Assert comment structure, and the canonical example makes the
mapping explicit:

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

Arrange is setup: it builds three substitutes, stubs the two that return
values, and constructs the engine under test. Act is exercise: one call,
`QuoteAsync`. Assert is verify: one assertion against the returned quote.
Teardown is the phase most of this book's tests never need to write, because
xUnit creates a fresh instance of the test class for every test method and
disposes it afterward; a test only writes an explicit teardown when it
acquires something the test class's own disposal would not reach, a
`CancellationTokenSource` a test creates directly, for instance, which this
book's suites wrap in a `using` declaration rather than a dedicated teardown
block:

```csharp
using var cancellationSource = new CancellationTokenSource();
```

A `using` declaration is teardown with the ceremony compiled away: the
`CancellationTokenSource` disposes at the end of the method regardless of how
the method exits, which is what an explicit teardown phase is for.

## The test-double taxonomy

Meszaros's taxonomy gives five names to the ways a test can replace a real
collaborator with something easier to control, in increasing order of how
much the double does.

**Dummy.** An object passed in only because a signature requires it, never
configured and never asked about. A test that constructs
`ShipmentRatingEngine` but only cares about the computed quote still has to
supply an `IAuditSink`, since the constructor requires one. Passing an
unconfigured substitute for it is a dummy:

```csharp
var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
```

The `DependencyBreakingTests` suite makes the same role explicit with
`out _`, discarding a collaborator the test does not need to inspect:

```csharp
var engine = BuildEngine(out var tariffs, out _, out var clock, out var references);
```

Neither test stubs the audit sink's behavior nor verifies a call against it.
It exists purely to satisfy the constructor.

**Stub.** An object that hands back a canned answer when called, so the
system under test has something to compute with. `ITaxPolicy`, stubbed with
`.Returns(...)`, is a stub:

```csharp
taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
```

The test above never verifies that `ApplyTax` was called. It only uses the
return value to check that the engine's final quote reflects the tax
applied. That is the defining trait of a stub: its job is to feed the system
under test, not to be interrogated afterward.

**Spy.** An object that records how it was called, so a test can inspect that
record after the fact rather than asserting through the double's own
verification API. NSubstitute does not expose a separate spy type, since
`Received()` already reads every substitute's call history on demand, but the
same recorded-history idea shows up directly in this book's hand-written
`InMemoryHandlingLedger`. Its `Entries` property is a spy-style window onto
every append a test drove through it:

```csharp
public IReadOnlyList<HandlingEvent> Entries
{
    get
    {
        lock (_entries)
        {
            return _entries.ToArray();
        }
    }
}
```

A test exercises the ledger through `AppendAsync`, then reads `Entries`
afterward to check what accumulated, the spy pattern of recording first and
asking questions later, rather than asserting a specific call happened as
the exercise phase runs.

**Mock.** An object whose calls are verified: not what it returns, but
whether, and how many times, and with what arguments, it was called. The same
`IAuditSink` substitute that was a dummy a moment ago becomes a mock the
instant a test verifies against it:

```csharp
await auditSink.Received(1).RecordAsync("C-1001", new Money(110.00m), Arg.Any<CancellationToken>());
```

This is the clearest illustration that the taxonomy describes a role in a
specific test, not a fixed property of an object. `Substitute.For<IAuditSink>()`
produces the same object regardless of whether a test ignores it, stubs it, or
verifies against it; what changes is what the test does with it afterward.
A substitute configured with `.Returns(...)` and read for its return value is
a stub. That same substitute, checked afterward with `Received(...)`,
is a mock. `ReceivedTests` in the testing-baseline example exercises exactly
this distinction: `ICarrierRates` and `ITaxPolicy` are stubbed for their
return values, while `IAuditSink` is verified for how it was called, inside
the same test.

**Fake.** A hand-written, working implementation that takes a shortcut
disqualifying it from production use, almost always trading a durable store
for an in-memory one. This book writes several. `InMemoryHandlingLedger`
appends to a list and hands out sequence numbers with
`Interlocked.Increment`, the concurrency-safe behavior a production ledger would
also need, but it keeps everything in a `List<HandlingEvent>` that vanishes
when the test process exits:

```csharp
public sealed class InMemoryHandlingLedger : IHandlingLedger
{
    private readonly List<HandlingEvent> _entries = [];
    private int _sequence;

    public async Task<int> AppendAsync(HandlingEvent e, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        var seq = Interlocked.Increment(ref _sequence);
        lock (_entries)
        {
            _entries.Add(e);
        }

        return seq;
    }
    // ...
}
```

`InMemoryBookingLedger` in the async-conventions example and
`FakeCarrierGateway` in the dependency-inversion example are the same idea
applied twice more: a working implementation of the interface, good
enough to drive a test or a sandbox environment through its actual behavior,
unsuitable for production because it never touches a database or a carrier's
network API. `FakeCarrierGateway`'s own documentation comment says as much:
it is "a deterministic stand-in for a real carrier, used in a sandbox or
demo environment where no production carrier contract exists yet," which is
exactly the shortcut-plus-real-behavior combination that separates a fake
from a stub.

Worth being precise about: not everything this book's code names `Fake` is a
fake by Meszaros's definition. The hand-written `FakeClock` used across the
seams, dependency-breaking, and Fowler-versus-Feathers examples has no real
behavior to speak of, it answers every call with the single value its
constructor was given:

```csharp
public sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow => utcNow;
}
```

That is a canned-answer provider with no logic, which by the taxonomy is a
hand-written stub wearing a `Fake` name. The distinction is not a flaw in the
book's naming, it reflects how the word "fake" is used colloquially for any
hand-rolled double. The difference still matters: a stub answers,
a fake works. `InMemoryHandlingLedger` keeps state and enforces
invariants (unique, monotonically increasing sequence numbers under
concurrent access); `FakeClock` has no invariant to enforce beyond returning
the value it was given.

## Reading a test for its doubles

Putting the taxonomy together, a single suite can use three or four kinds of
double across its tests, and the same substitute can switch roles inside one
test. `ReceivedTests` isolates `ShipmentRatingEngine` behind three
collaborators:

```csharp
var carrierRates = Substitute.For<ICarrierRates>();
carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
    .Returns(baseRate);
var taxPolicy = Substitute.For<ITaxPolicy>();
taxPolicy.ApplyTax(baseRate).Returns(taxedQuote);
auditSink = Substitute.For<IAuditSink>();
return new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
```

`carrierRates` and `taxPolicy` are stubs here, feeding the engine the numbers
the test needs to predict a result. `auditSink` starts out looking like a
dummy, built but not yet configured, and becomes a mock the moment a test
verifies against it with `Received(1)` or `DidNotReceive()`. None of the
three is a fake, because none of them has working logic behind it; they are
dynamic proxies that answer exactly what they were told to. Contrast that
with `DependencyBreakingTests`, which stubs `ITariffBook`, `IClock`, and
`IReferenceGenerator` the same way, then verifies `IAuditLog` as a mock with
an argument predicate:

```csharp
audit.Received(1).Record(Arg.Is<string>(message =>
    message.Contains("RQ-0002", StringComparison.Ordinal) &&
    message.Contains("C-9002", StringComparison.Ordinal)));
```

And contrast both with the concurrency examples, where a real
`InMemoryHandlingLedger` or `InMemoryBookingLedger` replaces the substitute
entirely, because the behavior under test, correct sequence assignment under
concurrent, overlapping calls, is exactly the behavior a dynamically
generated stub cannot provide. A substitute has no logic to race against. A
fake does, which is why the concurrency chapters reach for a hand-written one
instead.

## For deeper coverage

- Gerard Meszaros, [*xUnit Test Patterns: Refactoring Test
  Code*](https://www.informit.com/store/xunit-test-patterns-refactoring-test-code-9780132800051)
  (Addison-Wesley, 2007): the source of the four-phase test and the full
  dummy/stub/spy/mock/fake taxonomy this appendix applies.
- Microsoft Learn: [Best practices for writing unit
  tests](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
  for the Arrange-Act-Assert structure and .NET's own test-double guidance.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
