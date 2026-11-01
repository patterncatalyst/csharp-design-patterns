# 45-final-project

An end-to-end shipment workflow weaving four patterns over one
shipping-domain scenario: book a consignment, rate it, stow it, clear it
through customs, and invoice it.

- **Strategy.** `IRatingStrategy` is implemented by `StandardRatingStrategy`
  and `ExpressRatingStrategy`. Both compute the same ad-valorem-plus-weight
  base amount from the shared `RatingMath` constants; `ExpressRatingStrategy`
  multiplies that base amount by the express multiplier rather than
  recomputing it, so the two strategies can never drift apart on the shared
  part of the formula.
- **Factory.** `RatingStrategies.For` selects the strategy for a
  `ServiceLevel` so callers never construct one directly, and
  `ShipmentPipelineFactory.CreateDefault` is the composition root for the
  whole pipeline, wiring every stage and its collaborator with a plain `new`
  and no container.
- **Chain of Responsibility.** `ShipmentPipeline` runs a `BookingRequest`
  through an ordered list of `IShipmentStage` instances, `RatingStage`,
  `StowageStage`, `ClearanceStage`, `InvoiceStage`, threading an immutable
  `ShipmentContext` from one stage to the next. If `ClearanceStage` rejects
  the booking, the pipeline stops before `InvoiceStage` runs, so a rejected
  booking carries a quote and a stowage slot but no invoice.
- **Builder.** `InvoiceBuilder` accumulates `LineItem` entries and builds the
  final `Invoice`, with the total always equal to the sum of its lines.
  `InvoiceStage` uses it to emit a single freight line for a standard
  booking, or a freight line plus a separate service-surcharge line for an
  express one, so the invoice total always equals the quoted amount.

**Substitutable collaborators.** `StowageStage` depends on `ISlotAllocator`
rather than constructing `SequentialSlotAllocator` itself, and
`ClearanceStage` depends on `IClearanceCheck` rather than constructing
`StandardClearanceCheck` itself. Each seam earns its place: a test
substitutes `ISlotAllocator` with NSubstitute to verify `StowageStage` calls
it exactly once with the consignment ID, independent of the real allocation
sequence.

**What the tests prove.** `RatingTests` shows `RatingStrategies.For` selects
the correct strategy type for each `ServiceLevel`, that
`StandardRatingStrategy` computes the exact expected amount, and that
`ExpressRatingStrategy` applies the express multiplier to that same base
amount rather than a separately computed one. `PipelineTests` runs a
standard booking through the default pipeline end to end and asserts the
invoice total matches the strategy's own quoted amount, assigns the first
slot through `SequentialSlotAllocator`, substitutes an NSubstitute
`ISlotAllocator` into `StowageStage` directly, and proves a booking with a
non-positive declared value or an embargoed destination port is rejected at
clearance with no invoice produced, while a valid booking proceeds through
every stage. `InvoiceTests` exercises `InvoiceBuilder` directly to prove it
sums line items into the total, then runs an express booking through the
default pipeline to prove the invoice itemizes a freight line and a
separate service-surcharge line whose sum equals both the invoice total and
the original quoted amount.

Shipping domain, no containers: the whole pipeline is pure, synchronous,
in-memory computation, no network or I/O to fake out.

## Build and test

```sh
cd examples/45-final-project
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
