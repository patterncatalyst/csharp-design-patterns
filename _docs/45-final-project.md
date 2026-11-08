---
title: "Final project"
order: 45
layout: tutorial
sectionid: docs
part: "putting-it-together"
description: "A shipping-domain subsystem exercising patterns from every part, built end to end."
---

<!-- EXAMPLE (shipping domain): Booking, rating, stowage, clearance, invoice, one slice end to end. -->

## Learning objectives

By the end of this chapter you can:

- Trace a `BookingRequest` through `ShipmentPipeline.Run` across `RatingStage`, `StowageStage`, `ClearanceStage` and `InvoiceStage`, and say what each reads from and writes onto the immutable `ShipmentContext`.
- Explain how `RatingStrategies.For` picks `StandardRatingStrategy` or `ExpressRatingStrategy` by `ServiceLevel`, so `RatingStage` never branches on it.
- Show how `ClearanceResult.Rejected` stops the run before `InvoiceStage`, and name the two conditions `StandardClearanceCheck` rejects.
- Explain why a standard invoice carries one freight line and an express invoice a freight line plus a surcharge line, with the total equal to the `RateQuote` either way.
- Identify the stage that needs no named pattern beyond a constructor parameter, and treat that absence as a choice.

## Why this matters

Studying one pattern at a time shows what it buys on its own. A booking system doesn't get that isolation. One request to ship a consignment needs a rate, a slot, a customs check and an invoice, and each step is a candidate for its own seam, abstraction and test. Knowing what Strategy, Builder or a result type look like isn't the hard part. Deciding, stage by stage, which is worth its complexity in this request path, and which stage is better off with a constructor parameter and a direct call, is.

`ShipmentWorkflow` is one slice of a shipping system, booking through rating, stowage, clearance and invoicing, with only the seams a working system needs:

| Stage | Shape | Why |
| --- | --- | --- |
| Rating | Strategy plus a small Factory | Two rate schemes with paying customers; the pipeline shouldn't grow an `if` chain |
| Stowage | One interface parameter | One allocation rule; a test is the only second caller |
| Clearance | Result type | A customs rejection is a normal outcome, not an exception |
| Invoice | Builder | Line items vary by service level before they're summed |

Each seam justifies itself against a check that would fail without it, not against how the diagram looks.

{% include excalidraw.html name="45-final-project" caption="Figure 45.1: ShipmentPipeline.Run carries a BookingRequest through rating, stowage, clearance, and invoicing in order, producing an Invoice unless clearance halts the run first." %}

## An ordered pipeline from booking to invoice

The spine is a one-method interface that takes the context so far and returns the next one:

```csharp
public interface IShipmentStage
{
    ShipmentContext Process(ShipmentContext context);
}
```

`ShipmentContext` is the record that flows through every stage: the `BookingRequest` plus each stage's output, `RateQuote`, `StowageAssignment`, `ClearanceResult` and `Invoice`, all nullable and absent until their stage runs. No stage mutates a context. Each returns a new one built with `with`, so a context is one point in the booking's progress, not a mutable bag several stages reach into.

`ShipmentPipeline` folds the stages over the context and adds one piece of control flow:

```csharp
public sealed class ShipmentPipeline(IReadOnlyList<IShipmentStage> stages)
{
    public ShipmentContext Run(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new ShipmentContext(request);
        foreach (var stage in stages)
        {
            context = stage.Process(context);
            if (context.Clearance is { Cleared: false })
            {
                return context;
            }
        }

        return context;
    }
}
```

After every stage, if clearance has been recorded and failed, the run stops. The pipeline doesn't know which stages it holds or how many; it knows each implements `IShipmentStage` and that a rejected clearance is the one outcome worth stopping for. That's a chain of responsibility sized to the slice: an ordered list with one shared halting condition, not links that each decide whether to pass a request on.

The composition root wires the default pipeline:

```csharp
public static class ShipmentPipelineFactory
{
    public static ShipmentPipeline CreateDefault() => new(
    [
        new RatingStage(),
        new StowageStage(new SequentialSlotAllocator()),
        new ClearanceStage(new StandardClearanceCheck()),
        new InvoiceStage(),
    ]);
}
```

Every collaborator is built with `new` in one place, with no container or registration step to get wrong. Run through `CreateDefault` and `Run`, a standard booking's `Invoice.Total` equals what `RatingStrategies.For` computes directly, and a cleared booking ends with `Quote`, `Stowage`, `Clearance` and `Invoice` all populated.

## Strategy and Factory at rating

`RatingStage` only delegates:

```csharp
public sealed class RatingStage : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var quote = RatingStrategies.For(context.Booking.Service).Quote(context.Booking);
        return context with { Quote = quote };
    }
}
```

`IRatingStrategy` names the operation both schemes implement, `Quote(BookingRequest)`, and `RatingStrategies.For` is the small factory that maps a `ServiceLevel` to an implementation:

```csharp
public static class RatingStrategies
{
    public static IRatingStrategy For(ServiceLevel service) => service switch
    {
        ServiceLevel.Standard => new StandardRatingStrategy(),
        ServiceLevel.Express => new ExpressRatingStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown service level."),
    };
}
```

The interface exists because two rate schemes exist, not to show off the pattern. `StandardRatingStrategy` computes the declared-value and per-kilogram components from `RatingMath` with no surcharge. `ExpressRatingStrategy` computes the same base and multiplies by `RatingMath.ExpressMultiplier`. Both read the same constants, so a rate change updates both at once.

| Booking: 10,000 declared, 500 kg | Quote |
| --- | --- |
| Standard | `1,400.00m` |
| Express | `1,960.00m`, exactly the standard amount times `1.40m` |

`RatingStage` never has to know a second strategy exists. A third service level means a new `IRatingStrategy` and one arm in `RatingStrategies.For`; `RatingStage`, `ShipmentPipeline` and every downstream stage stay as they are. A Strategy chosen by a small Factory fits a decision with more than one real answer and a caller that shouldn't care which it got.

## A stage that needs no pattern at all

{% include excalidraw.html name="45-patterns-woven" caption="Figure 45.2: RatingStage selects an IRatingStrategy through RatingStrategies.For, ClearanceStage returns a ClearanceResult, and InvoiceStage builds with InvoiceBuilder; StowageStage needs none of it." %}

`StowageStage` is the clearest seam in the slice that doesn't need a name:

```csharp
public sealed class StowageStage(ISlotAllocator allocator) : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var consignmentId = context.Booking.ConsignmentId;
        var stowage = new StowageAssignment(consignmentId, allocator.Allocate(consignmentId));
        return context with { Stowage = stowage };
    }
}
```

`ISlotAllocator` has one method, `Allocate(string consignmentId)`, and one production implementation, `SequentialSlotAllocator`, which counts up from `SLOT-001` once per call for the life of the instance. There's no Strategy because nothing is chosen at runtime, no Factory because nothing is selected, and no result type because allocation doesn't fail in a way the pipeline branches on. Constructor injection is the whole seam. A fresh allocator hands out `SLOT-001` first, and an NSubstitute double returning `SLOT-999` replaces it without touching `StowageStage`, receiving exactly one `Allocate` call. That substitution is the justification for the interface. A Strategy here, with one implementation and no competing rule, would be a seam nobody pays for.

## A result type that halts the pipeline

`ClearanceStage` calls an injected `IClearanceCheck` and records its `ClearanceResult` on the context. A booking can stop here without an exception. `ClearanceResult` is a record with two named factory members instead of a boolean and a side channel for the reason:

```csharp
public sealed record ClearanceResult(bool Cleared, string? Reason)
{
    public static ClearanceResult Clear() => new(true, null);

    public static ClearanceResult Rejected(string reason) => new(false, reason);
}
```

Both factories produce the same two-field record, so results compare by value. `StandardClearanceCheck` rejects for two reasons, a non-positive declared value or an embargoed destination port, and clears everything else:

```csharp
public sealed class StandardClearanceCheck : IClearanceCheck
{
    private static readonly HashSet<string> EmbargoedDestinations = ["ZZZ"];

    public ClearanceResult Check(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DeclaredValue <= 0m)
        {
            return ClearanceResult.Rejected("declared value must be positive");
        }

        if (EmbargoedDestinations.Contains(request.DestinationPort))
        {
            return ClearanceResult.Rejected("destination port is embargoed");
        }

        return ClearanceResult.Clear();
    }
}
```

A failed clearance is one of two expected outcomes, and `ShipmentPipeline.Run` already checks for it after every stage. A booking with a zero declared value ends with `Cleared` false and a null `Invoice`; one bound for the embargoed `ZZZ` comes back with the reason `"destination port is embargoed"`. No exception to catch and no log to read: the halted run is the context `Run` returned, with `Invoice` unset because `InvoiceStage` never ran.

{% include excalidraw.html name="45-clearance-halts-the-slice" caption="Figure 45.3: A cleared booking reaches InvoiceStage and produces an Invoice. A rejected one stops at ClearanceStage with Cleared false, and the pipeline never builds one." %}

## Builder at invoice

`InvoiceStage` runs only after clearance passes. It turns the `RateQuote` into a billing document with the right line items for the service level:

```csharp
public sealed class InvoiceStage : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var booking = context.Booking;
        var quote = context.Quote ?? throw new InvalidOperationException(
            "InvoiceStage requires a quote; run RatingStage first.");

        var builder = new InvoiceBuilder();
        if (booking.Service == ServiceLevel.Express)
        {
            var standardBase = new StandardRatingStrategy().Quote(booking).Amount;
            var surcharge = quote.Amount - standardBase;
            builder.AddLineItem("Freight", standardBase);
            builder.AddLineItem("Express service surcharge", surcharge);
        }
        else
        {
            builder.AddLineItem("Freight", quote.Amount);
        }

        return context with { Invoice = builder.Build(booking.ConsignmentId) };
    }
}
```

A standard booking gets one freight line equal to the quote. An express booking gets a freight line recomputed at the standard rate and a surcharge line for the difference. Recomputing the standard amount keeps `ShipmentContext` from carrying a value only `InvoiceStage` needs, at the cost of one call to a stateless strategy.

`InvoiceBuilder` makes either shape the same operation:

```csharp
public sealed class InvoiceBuilder
{
    private readonly List<LineItem> _lineItems = [];

    public InvoiceBuilder AddLineItem(string description, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(description);
        _lineItems.Add(new LineItem(description, amount));
        return this;
    }

    public Invoice Build(string consignmentId)
    {
        ArgumentNullException.ThrowIfNull(consignmentId);

        var lineItems = _lineItems.ToArray();
        return new Invoice(consignmentId, lineItems, lineItems.Sum(item => item.Amount));
    }
}
```

`AddLineItem` returns the builder so calls chain, and `Build` sums the lines into `Invoice.Total` so no caller adds them up. An express booking run through the full pipeline produces exactly a `"Freight"` line and an `"Express service surcharge"` line, summing to the `RateQuote.Amount` rating produced. A Builder fits because the line count varies, one for standard and two for express, and the assembler shouldn't need to know in advance how many it will hold.

## Right-sized, seam by seam

Each stage answered the same two questions against its own shape: does more than one real behavior need to live behind this seam, and would a check fail if the seam were removed?

| Stage | More than one real behavior? | Pattern kept |
| --- | --- | --- |
| Rating | Yes: two schemes, a third plausible | Strategy and Factory |
| Stowage | No: one rule, substituted only for testing | None beyond a constructor parameter |
| Clearance | Yes: cleared or rejected, both normal | Result type |
| Invoice | Yes: line count depends on runtime data | Builder |

None of these generalizes to "always do this at a rating step" or "never put an interface on an allocator." Where both answers are yes, the pattern stays. Where they're no, a constructor parameter is the complete design, and anything more builds for a requirement nobody has stated.

That's also why `ShipmentPipeline` stays a plain ordered list. Four stages, one halting condition and a composition root using `new` run the slice correctly and let every stage be tested in place and alone. Dozens of stages, conditional branches between them or out-of-order stages would justify a richer pipeline. Four stages that always run in the same sequence don't.

## Build, run, observe

```sh
cd examples/45-final-project
dotnet test
```

All 12 tests pass: four on rating (both pinned amounts, express costing more, strategy selection by service level), six on the pipeline (the end-to-end total, the first slot, a substituted allocator, both clearance rejections, a fully populated cleared context) and two on invoicing (the builder's sum and the itemized express invoice).

## What the slice shows

Every seam in the slice exists because a check would fail without it, and that's easier to see with the four stages side by side than while writing any one of them. `RatingStage` keeps its Strategy because strategy selection and the two pinned amounts depend on it. `StowageStage` could have grown a Strategy as soon as someone imagined a second allocation rule; it stayed a single interface parameter because no second rule has needed one, and a substituted double justifies an interface where an imagined rule doesn't. One question, asked four times, gave four answers: a Strategy at rating, a constructor parameter at stowage, a result type at clearance and a Builder at invoicing.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a pipeline stage.** "Make the shipment pipeline more extensible" leaves open which stages get a pattern and which get a plain constructor parameter. State each:

> Each `IShipmentStage.Process` returns `context with { ... }` on the immutable `ShipmentContext`, and `ShipmentPipeline.Run` stops after any stage once `Clearance` is `{ Cleared: false }`. `RatingStage` picks an `IRatingStrategy` through `RatingStrategies.For(context.Booking.Service)`. `StowageStage` takes one `ISlotAllocator` through its constructor; don't give it a strategy, a factory or a result type. `ClearanceStage` records the `ClearanceResult` from `IClearanceCheck`. `InvoiceStage` builds with `InvoiceBuilder`. Keep `ShipmentPipelineFactory.CreateDefault` as four `new` calls, with no container.

Run `dotnet test --project ShipmentWorkflow.Tests --filter PipelineTests`; `Slot_allocator_can_be_substituted` must still pass.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, Vlissides, [*Design Patterns: Elements of Reusable
  Object-Oriented Software*](https://www.oreilly.com/library/view/design-patterns-elements/0201633612/)
  (Addison-Wesley, 1994): the source definitions for the Strategy, Factory,
  Chain of Responsibility, and Builder shapes this slice weaves together,
  each applied only where its own stage needed it.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html),
  2nd ed. (Addison-Wesley, 2018): the discipline behind right-sizing a seam,
  adding an abstraction when duplication or a real second case justifies it
  rather than ahead of either one.
- Microsoft Learn, [Dependency injection in
  .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection):
  constructor injection as the idiom behind every stage in this pipeline
  receiving its collaborators, `ISlotAllocator` and `IClearanceCheck`
  included, rather than constructing them itself.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records):
  the value semantics and nondestructive `with` mutation that
  `ShipmentContext`, `ClearanceResult`, and every other record in this slice
  rely on.
- Microsoft Learn, [Pattern
  matching](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching):
  the property-pattern check, `context.Clearance is { Cleared: false }`,
  that `ShipmentPipeline.Run` uses to decide when to stop.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
