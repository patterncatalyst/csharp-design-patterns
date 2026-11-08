---
title: "xUnit test patterns"
order: 50
layout: tutorial
sectionid: docs
part: appendices
appendix: "D"
description: "The four-phase test and test-double taxonomy from Meszaros, applied to xUnit and NSubstitute."
---

## Learning objectives

By the end of this appendix you can:

- Name the four phases of a test (setup, exercise, verify, teardown) and map
  them onto Arrange/Act/Assert.
- Define the five test doubles in Meszaros's taxonomy: dummy, stub, spy, mock
  and fake.
- Tell which role an NSubstitute substitute plays, since one substitute can
  be a stub in one line and a mock in the next.
- Recognize a hand-written in-memory collaborator as a fake, and say why it
  needs tests of its own.

## Why this matters

Gerard Meszaros's *xUnit Test Patterns* named the four-phase test and the
test-double taxonomy of dummy, stub, spy, mock and fake. Without that
vocabulary everything that isn't the real collaborator gets called "a mock",
and the name stops telling you what a failure means:

| Double that failed | What the failure says |
| --- | --- |
| Stub | The collaborator's output drove the wrong behavior. |
| Mock | The system under test didn't call its collaborator as required. |
| Fake | The state the system under test accumulated is wrong. |

## The four-phase test

| Meszaros phase | Comment in the code | What happens |
| --- | --- | --- |
| Setup | `// Arrange` | Build the system under test and its collaborators in the state you need. |
| Exercise | `// Act` | Call the one behavior under test. |
| Verify | `// Assert` | Check a return value, a thrown exception or an interaction. |
| Teardown | usually none | Release anything setup acquired that would leak into the next test. |

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

Arrange builds three substitutes, stubs the two that return values and
constructs the engine. Act is one call. Assert is one check on the quote.

Teardown is rarely written, because xUnit creates a fresh test-class instance
for every test method and disposes it afterward. A resource a test creates
itself, such as a `CancellationTokenSource`, takes a `using` declaration
instead of a teardown block:

```csharp
using var cancellationSource = new CancellationTokenSource();
```

The source is disposed at the end of the method however it exits, which is
the job of a teardown phase.

## The test-double taxonomy

| Double | What it does | NSubstitute or C# form |
| --- | --- | --- |
| Dummy | Fills a parameter; never configured or checked | An unconfigured `Substitute.For<T>()`, or `out _` |
| Stub | Returns canned answers to feed the system under test | `.Returns(...)` |
| Spy | Records calls or state for inspection afterward | Call history behind `Received()`; a read-only `Entries` view on a fake |
| Mock | Is verified for how it was called | `Received(n)`, `DidNotReceive()`, `Arg.Is<T>(...)` |
| Fake | A working implementation with a shortcut, usually in-memory storage | A hand-written class implementing the interface |

The role belongs to what a test does with the object, not to the object.
`Substitute.For<IAuditSink>()` builds the same proxy whether a test ignores
it, stubs it or verifies it.

### Dummy

A constructor needs an `IAuditSink`, but a test may care only about the quote.
An unconfigured substitute fills the slot:

```csharp
var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());
```

`out _` discards a collaborator a helper builds that a test never inspects:

```csharp
var engine = BuildEngine(out var tariffs, out _, out var clock, out var references);
```

### Stub

A stubbed `ITaxPolicy` feeds the engine a value. Nothing verifies that
`ApplyTax` was called; the return value only shapes the quote:

```csharp
taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
```

### Spy

NSubstitute has no spy type, because `Received()` reads any substitute's call
history on demand. A hand-written fake can expose the same record directly.
`InMemoryHandlingLedger.Entries` returns everything appended, so you exercise
through `AppendAsync` and check what accumulated afterward:

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

### Mock

Verify a call on the audit sink and it's a mock:

```csharp
await auditSink.Received(1).RecordAsync("C-1001", new Money(110.00m), Arg.Any<CancellationToken>());
```

A typical setup stubs `ICarrierRates` and `ITaxPolicy` for their return values
and verifies `IAuditSink` for how it was called, all in one test.

### Fake

`InMemoryHandlingLedger` appends to a list and assigns sequence numbers with
`Interlocked.Increment`, the concurrency-safe behavior a production ledger
also needs, but its `List<HandlingEvent>` vanishes when the process exits:

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

`InMemoryBookingLedger` and `FakeCarrierGateway` follow the same idea: a
working implementation, good enough to drive a test or a sandbox through real
behavior, unfit for production because it never touches a database or a
carrier's network API. `FakeCarrierGateway`'s documentation comment calls it
"a deterministic stand-in for a real carrier, used in a sandbox or demo
environment where no production carrier contract exists yet."

A class named `Fake` isn't always a fake. `FakeClock` returns the single value
its constructor was given:

```csharp
public sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow => utcNow;
}
```

With no logic, it's a hand-written stub; "fake" is commonly used for any
hand-rolled double. A stub answers, a fake works. `InMemoryHandlingLedger`
keeps state and enforces an invariant: unique, monotonically increasing
sequence numbers under concurrent access. `FakeClock` has no invariant
beyond returning its value. Because a fake has behavior, it needs tests of
its own, separate from the code that consumes it.

## Reading a test for its doubles

One setup can hold several roles. Here `ShipmentRatingEngine` is isolated
behind three collaborators:

```csharp
var carrierRates = Substitute.For<ICarrierRates>();
carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
    .Returns(baseRate);
var taxPolicy = Substitute.For<ITaxPolicy>();
taxPolicy.ApplyTax(baseRate).Returns(taxedQuote);
auditSink = Substitute.For<IAuditSink>();
return new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
```

`carrierRates` and `taxPolicy` are stubs that feed the engine numbers the
test can predict. `auditSink` is a dummy until a test verifies it with
`Received(1)` or `DidNotReceive()`, and then it's a mock. None is a fake,
since none has working logic.

A mock can also take an argument predicate. Here `ITariffBook`, `IClock` and
`IReferenceGenerator` are stubs and `IAuditLog` is the mock:

```csharp
audit.Received(1).Record(Arg.Is<string>(message =>
    message.Contains("RQ-0002", StringComparison.Ordinal) &&
    message.Contains("C-9002", StringComparison.Ordinal)));
```

Concurrent code needs a fake. When the behavior under test is correct
sequence assignment under overlapping calls, a real `InMemoryHandlingLedger`
or `InMemoryBookingLedger` replaces the substitute: a dynamic proxy has no
logic to race against.

## For deeper coverage

- Gerard Meszaros, [*xUnit Test Patterns: Refactoring Test
  Code*](https://www.informit.com/store/xunit-test-patterns-refactoring-test-code-9780132800051)
  (Addison-Wesley, 2007): the source of the four-phase test and the full
  dummy/stub/spy/mock/fake taxonomy this appendix applies.
- Microsoft Learn: [Best practices for writing unit
  tests](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
  for the Arrange-Act-Assert structure and .NET's own test-double guidance.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
