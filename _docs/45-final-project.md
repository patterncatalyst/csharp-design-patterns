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

- Trace a `BookingRequest` through `ShipmentPipeline.Run` across
  `RatingStage`, `StowageStage`, `ClearanceStage`, and `InvoiceStage`, and
  explain what each stage reads from and writes onto the immutable
  `ShipmentContext` it is handed.
- Explain how `RatingStrategies.For` selects between `StandardRatingStrategy`
  and `ExpressRatingStrategy` by `ServiceLevel`, and why `RatingStage` itself
  never branches on that value.
- Show how `ClearanceResult.Rejected` stops `ShipmentPipeline.Run` before
  `InvoiceStage` runs, and name the two conditions `StandardClearanceCheck`
  rejects a booking for.
- Read `InvoiceBuilder` and explain why a standard booking's invoice carries
  one freight line while an express booking's invoice carries a freight line
  and a separate surcharge line, with the total equal to the `RateQuote`
  either way.
- Identify which stage in the pipeline needs no named pattern at all beyond
  a constructor parameter, and explain why that absence is as considered a
  choice as every seam that is present.

## Why this matters

Most of this book studies one pattern at a time, in isolation, because that
is the only way to see what a pattern buys you on its own, before the cost of
everything else in a working system buries the lesson. A booking system does
not get that luxury. A single request to ship a consignment needs a rate
computed, a slot assigned, a customs check run, and an invoice produced, and
every one of those steps is a candidate for its own seam, its own
abstraction, its own test. The hard part is not knowing what Strategy or
Builder or a result type look like on their own. The hard part is deciding,
stage by stage, which of them is worth its complexity in this particular
request path and which stage is better off with nothing more than a
constructor parameter and a direct call.

That decision is the subject of this chapter. `ShipmentWorkflow` is one
slice of a shipping system, booking through rating, stowage, clearance, and
invoicing, assembled from the same four or five seams a working system
needs rather than a showcase that reaches for every pattern this book has
covered. Rating gets a Strategy and a small Factory because two different
rate schemes are real, both with paying customers, and a caller needs to
pick between them without an `if` chain creeping into the pipeline.
Clearance gets a result type because customs decisions are not exceptions;
they are a normal outcome a caller is expected to handle. Invoicing gets a
Builder because an invoice accumulates line items that vary by service
level before it can be summed into a total. Stowage gets none of that: a
single interface parameter
is already the right size for an allocation that assigns one slot and
reports it. Reading the four stages side by side shows what a well-sized
slice looks like when every seam has to justify itself against a passing
test, not against how impressive the resulting diagram looks.

{% include excalidraw.html name="45-final-project" caption="Figure 45.1: ShipmentPipeline.Run carries a BookingRequest through rating, stowage, clearance, and invoicing in order, producing an Invoice unless clearance halts the run first." %}

## An ordered pipeline from booking to invoice

The spine of the slice is `IShipmentStage`, a one-method interface that
takes the context built so far and returns the next one:

```csharp
public interface IShipmentStage
{
    ShipmentContext Process(ShipmentContext context);
}
```

`ShipmentContext` is the record that flows through every stage: the original
`BookingRequest` plus whatever each stage has produced, `RateQuote`,
`StowageAssignment`, `ClearanceResult`, and `Invoice`, all nullable and all
absent until the stage responsible for them runs. Nothing in the pipeline
mutates a context in place. Each stage receives one and returns a new one
built with `with`, so a context captures a single point in the booking's
progress rather than a mutable bag of fields several stages reach into at
once.

`ShipmentPipeline` folds a list of stages over that context, in order, and
adds exactly one piece of control flow beyond the fold itself:

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

That `if` is the whole of the pipeline's decision logic: after every stage
runs, check whether clearance has been recorded and failed, and stop early
if it has. Nothing about `ShipmentPipeline` knows which concrete stages it
holds or how many of them there are. It knows only that each one implements
`IShipmentStage` and that a rejected clearance is the one outcome worth
cutting the run short for. That is a chain of responsibility sized to what
this shipping slice needs: an ordered list walked in sequence, with one
shared halting condition, rather than a chain where each link decides for
itself whether to pass a request along.

`ShipmentPipelineFactory.CreateDefault` is the composition root that wires
the default pipeline together:

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

Every collaborator is constructed with a plain `new`, in one place, with no
container and no registration step to get wrong. `PipelineTests.End_to_end_booking_produces_an_invoice_with_the_rated_total`
runs a `StandardBooking` through the default pipeline and asserts that the
resulting `Invoice.Total` equals the amount `RatingStrategies.For` would
compute directly, and `Cleared_booking_proceeds_through_every_stage` checks
that a cleared booking leaves every one of `Quote`, `Stowage`, `Clearance`,
and `Invoice` populated on the final context. Both tests exercise the
pipeline exactly the way a caller would, through `CreateDefault` and `Run`,
with no stage replaced or inspected individually.

## Strategy and Factory at rating

`RatingStage` is the first stage to run, and its own body does nothing but
delegate:

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

`IRatingStrategy` names the one operation both rate schemes implement,
`Quote(BookingRequest)`, and `RatingStrategies.For` is the small factory that
turns a `ServiceLevel` into the right implementation:

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

This Strategy is worth its interface because two distinct rate schemes
exist, not because a single calculation was split up to demonstrate the
pattern.
`StandardRatingStrategy` computes the declared-value and per-kilogram
components from `RatingMath` with no surcharge.
`ExpressRatingStrategy` computes the identical base amount and then
multiplies it by `RatingMath.ExpressMultiplier`. Both read the same shared
constants rather than each hard-coding its own copy of the ad-valorem rate,
the per-kilogram rate, or the express multiplier, so a rate change updates
one source for both strategies at once. `RatingTests.Standard_rating_computes_the_expected_amount`
pins the standard quote at `1,400.00m` for a ten-thousand-dollar, five
hundred kilogram booking, `Express_rating_applies_the_express_multiplier`
pins the express quote at `1,960.00m` for the same booking and asserts it
equals the standard amount times `1.40m` exactly, and
`Rating_strategy_is_selected_by_service_level` checks that `RatingStrategies.For`
returns a `StandardRatingStrategy` instance for one service level and an
`ExpressRatingStrategy` instance for the other. `Express_booking_costs_more_than_the_same_standard_booking`
closes the loop by comparing the two quotes directly rather than trusting
that distinct numbers imply distinct behavior.

What makes this seam worth its interface, rather than a single method with
an `if` branch for the two service levels, is that `RatingStage` never has
to know a second strategy exists. Adding a third service level later means
writing a new `IRatingStrategy` implementation and adding one arm to
`RatingStrategies.For`; it does not mean touching `RatingStage`,
`ShipmentPipeline`, or any stage downstream of rating. A Strategy selected
by a small Factory is exactly the right size for a decision that has more
than one real answer and a caller, here `RatingStage`, that should not have
to care which answer it got.

## A stage that needs no pattern at all

{% include excalidraw.html name="45-patterns-woven" caption="Figure 45.2: RatingStage selects an IRatingStrategy through RatingStrategies.For, ClearanceStage returns a ClearanceResult, and InvoiceStage builds with InvoiceBuilder; StowageStage needs none of it." %}

`StowageStage` sits between rating and clearance, and it is the clearest
example in this slice of a seam that does not need a name:

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

`ISlotAllocator` has one method, `Allocate(string consignmentId)`, and one
production implementation, `SequentialSlotAllocator`, which counts up from
`SLOT-001` once per call for the lifetime of the instance. There is no
Strategy here because there is nothing to choose between at runtime, no
Factory because there is nothing to select, and no result type because
allocating a slot does not fail in a way the rest of the pipeline needs to
branch on. Constructor injection of `ISlotAllocator` is the entire seam, and
it is enough: `PipelineTests.Stowage_stage_assigns_the_first_slot` confirms
a fresh `SequentialSlotAllocator` hands out `SLOT-001` first, and
`Slot_allocator_can_be_substituted` replaces the real allocator with an
NSubstitute double returning `SLOT-999`, then asserts both the substituted
result and that `Allocate` was called exactly once. That second test is the
actual justification for the interface: not a pattern diagram, but a
concrete need to swap the allocator out during a test without touching
`StowageStage` itself. A stage that needed a Strategy here, with one
production implementation and no second rule set competing for the same
slot, would be adding a seam nobody is paying for.

## A result type that halts the pipeline

`ClearanceStage` is the stage where a booking can stop making progress
without raising an exception:

```csharp
public sealed class ClearanceStage(IClearanceCheck check) : IShipmentStage
{
    public ShipmentContext Process(ShipmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var clearance = check.Check(context.Booking);
        return context with { Clearance = clearance };
    }
}
```

`ClearanceResult` is a record with two named factory members rather than a
boolean return value and a side channel for the reason:

```csharp
public sealed record ClearanceResult(bool Cleared, string? Reason)
{
    public static ClearanceResult Clear() => new(true, null);

    public static ClearanceResult Rejected(string reason) => new(false, reason);
}
```

`Clear()` and `Rejected(reason)` are the only two ways to construct a
`ClearanceResult` that read naturally at the call site, and both end up as
the same two-field record underneath, so a test comparing results compares
values rather than reasoning about which constructor overload ran.
`StandardClearanceCheck` rejects a booking for exactly two reasons, a
declared value that is not positive or a destination port on a fixed
embargo list, and clears everything else:

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

The result type is worth its shape because a failed clearance check is not
an exceptional circumstance for this pipeline; it is one of two expected
outcomes, and `ShipmentPipeline.Run` already checks for it after every
stage. `PipelineTests.Clearance_rejection_halts_the_pipeline_before_invoicing`
sets `DeclaredValue` to zero on an otherwise valid booking, runs the default
pipeline, and asserts the final context carries a `Clearance` with
`Cleared` false and an `Invoice` that is null. `Embargoed_destination_is_rejected_at_clearance`
does the same with the destination port set to the embargoed code and
checks that the specific rejection reason comes back unchanged. Neither
test needs to catch an exception or inspect a log; the halted run is just
the context `Run` returned, with `Invoice` never set because `InvoiceStage`
never ran.

{% include excalidraw.html name="45-clearance-halts-the-slice" caption="Figure 45.3: A cleared booking reaches InvoiceStage and produces an Invoice. A rejected one stops at ClearanceStage with Cleared false, and the pipeline never builds one." %}

## Builder at invoice

`InvoiceStage` is the last stage in the default pipeline, and it only runs
at all when clearance has passed. Its job is to turn the `RateQuote` that
`RatingStage` produced into a billing document with the right line items for
the booking's service level:

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

A standard booking gets one freight line equal to the quote. An express
booking gets two lines: a freight line recomputed at the standard rate, and
a surcharge line that is the difference between the express quote and that
standard base. Recomputing the standard amount here rather than storing it
on the context keeps `ShipmentContext` from carrying a value only
`InvoiceStage` needs, at the cost of one extra call to a strategy that has
no state to construct.

`InvoiceBuilder` is the seam that makes itemizing either shape of invoice
the same operation:

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

`AddLineItem` returns the builder itself so calls can chain, and `Build`
closes the accumulation by summing every line item into the `Invoice.Total`
rather than asking a caller to add the lines up separately. That sum is the
property both invoice tests check. `InvoiceTests.Invoice_builder_sums_line_items_into_the_total`
drives the builder directly with two line items and asserts the total is
their sum, with no pipeline involved. `Express_invoice_itemizes_freight_and_the_service_surcharge`
runs an express booking through the full default pipeline and asserts the
resulting invoice carries exactly a `"Freight"` line and an
`"Express service surcharge"` line, that the line items sum to the invoice
total, and that the invoice total equals the `RateQuote.Amount` the pipeline
produced earlier. A Builder is the right shape here because the number of
line items differs by service level, one for standard, two for express, and
the type that assembles them should not have to know in advance how many
lines it will end up holding.

## Right-sized, seam by seam

Lay the four stages next to each other and a pattern in the choice of
patterns becomes visible. `RatingStage` gets Strategy and Factory because
two rate schemes are real and a third is plausible. `StowageStage` gets
nothing beyond an interface parameter because one allocation rule exists and
the only reason to substitute it is testing, which constructor injection
already covers. `ClearanceStage` gets a result type because a rejected
booking is a normal branch in the pipeline's logic, not a failure the system
needs to unwind from. `InvoiceStage` gets a Builder because the number of
billable lines depends on data the method only knows at runtime. None of the
four decisions generalizes to "always do this at a rating step" or "never
use an interface for an allocator." Each one answers the same question
against the specific shape of its own stage: does more than one real
behavior need to exist behind this seam, and does a test exist that would
fail if the seam were removed. Where the answer to both is yes, as at
rating, clearance, and invoicing, the pattern stays. Where the answer is no,
as at stowage, a constructor parameter is the complete design, and adding
more than that would be building for a requirement nobody has stated.

That is also why `ShipmentPipeline` itself stays a plain ordered list rather
than growing into something more elaborate. Four stages, one shared halting
condition, and a composition root that wires them with `new` is enough to
run the slice correctly and to test every stage both in place and in
isolation. A booking system handling dozens of stages, conditional branches
between them, or stages that need to run out of order would justify a
richer pipeline abstraction. Four stages that always run in the same
sequence do not, and the tests in this slice check the sequence that exists
rather than a flexibility nobody asked the pipeline to have.

## Build, run, observe

The example behind this chapter is `examples/45-final-project`: the
`ShipmentWorkflow` namespace, its four pipeline stages, the two rating
strategies, the clearance result type, and the invoice builder, assembled
by `ShipmentPipelineFactory.CreateDefault` with no container. It is
standalone-testable from its own directory:

```sh
cd examples/45-final-project
dotnet test
```

Observed locally with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 12
  failed: 0
  succeeded: 12
  skipped: 0
```

The twelve cases split across three test classes. `RatingTests` contributes
four: the standard and express amounts pinned to exact values, the fact
that an express quote costs more than the matching standard quote, and the
check that `RatingStrategies.For` returns the right concrete type for each
service level. `PipelineTests` contributes six: an end-to-end run asserting
the invoice total matches the rated amount, the first slot assigned by a
fresh `SequentialSlotAllocator`, a substituted allocator proving
`StowageStage` depends only on the `ISlotAllocator` interface, a rejection
on a non-positive declared value, a rejection on an embargoed destination,
and a fact confirming a cleared booking populates every field on the final
context. `InvoiceTests` contributes the remaining two: the builder's line
items summing correctly on their own, and a full express run proving the
itemized freight and surcharge lines sum to the same total the rating stage
quoted.

## What the slice shows

Every seam in this slice is there because a test would fail without it, and
that discipline is easier to see once the four stages sit side by side than
it is while writing any one of them on its own. `RatingStage` could have
reached for a Strategy the moment a second rate existed even if nothing had
exercised the choice; instead the seam exists because
`Rating_strategy_is_selected_by_service_level` and the two pinned-amount
tests depend on it directly. `StowageStage` could have grown a Strategy of
its own the moment someone imagined a second allocation rule; instead it
stayed a single interface parameter because no second rule has ever needed
one, and a substituted double in a test is a complete justification for an
interface that an imagined future rule is not. The same question, does a
real behavior or a real test sit behind this seam, produced a Strategy at
rating, a plain constructor parameter at stowage, a result type at
clearance, and a Builder at invoicing, four different answers from asking
one consistent question four times.

<blockquote class="ai-sidebar" markdown="1">
An AI assistant is useful for assembling a slice like this one: asked to
wire four stages behind a shared interface, thread an immutable
context through them with `with` expressions, and add a halting check after
each stage runs, it can produce working code and a first pass at the tests
in a fraction of the time a careful human pass would take, and it rarely
tires of writing the repetitive boilerplate a composition root like
`ShipmentPipelineFactory.CreateDefault` needs. What it will not reliably do
on its own is draw the line between a stage that deserves a Strategy and one
that deserves nothing more than a constructor parameter. Asked to make a
pipeline "more extensible," a model will often add an interface and a
factory to every stage uniformly, including the one stage where a single
implementation and a direct call were already the right answer, because
uniform abstraction reads as more thorough than selective abstraction even
when it is not. Deciding which seam is worth keeping, and confirming that
decision with a test that would fail if the seam were removed, stays a
human call. The assistant can draft the scaffolding fast; only a reviewer
who understands why `StowageStage` needs less than `ClearanceStage` can tell
the slice when to stop adding patterns.
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
