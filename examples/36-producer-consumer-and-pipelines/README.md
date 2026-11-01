# 36-producer-consumer-and-pipelines

A bounded handling-event ingest pipeline that separates producing work from
consuming it, over the same `HandlingEventPipeline` seam, so the pattern and
its mechanism can be examined independently.

- **Producer-Consumer, the pattern.** Any number of producers submit
  `HandlingEvent` records describing a carrier or terminal's handling report
  (receive, load, unload, customs, claim) against a `TrackingId`. A fixed pool
  of consumer tasks drains them. Producers and consumers never call each other
  directly; they only ever touch the shared channel between them.
- **`System.Threading.Channels`, the mechanism.** `HandlingEventPipeline` wraps
  a bounded `Channel<HandlingEvent>` created with `BoundedChannelFullMode.Wait`.
  `SubmitAsync` is `_channel.Writer.WriteAsync`, so a producer that outruns the
  consumers waits for room instead of growing an unbounded buffer or dropping
  work. Each consumer task runs `await foreach (var e in
  _channel.Reader.ReadAllAsync())`, which keeps pulling events until the
  writer side is completed and the channel has drained.
- **Deterministic completion.** `CompleteAndDrainAsync` calls
  `_channel.Writer.Complete()` and then `Task.WhenAll` over every consumer
  task. That is the pipeline's only way to decide it is done: no polling, no
  wall-clock wait, just "every consumer's `await foreach` has observed the
  completed writer and returned."
- **A gated consumer hook for backpressure.** The optional `onEvent` hook lets
  a caller intercept each event as it is processed. The backpressure test uses
  it to park the single consumer mid-event behind a
  `TaskCompletionSource<bool>`, so the test can drive the capacity-1 channel
  into a known-full state and prove a pending `SubmitAsync` call has not
  completed, before releasing the gate and letting the pipeline finish.

**What the tests prove.** `Every_event_produced_is_consumed_exactly_once` runs
four producers and four consumers over a capacity-8 channel and checks the
multiset of observed events, compared after sorting rather than by submission
order, matches exactly what was produced: no event is lost, duplicated, or
invented. `A_single_producer_order_is_preserved_by_a_single_consumer` drops to
one producer and one consumer and asserts the observed sequence numbers come
back in the exact order they were submitted, which a channel only guarantees
when a single consumer is draining it. `A_full_bounded_channel_applies_
backpressure_to_the_producer` proves a full capacity-1 channel genuinely
blocks a writer: a pending submission's `Task.IsCompleted` is asserted false
while the single consumer is parked, then found to complete only after the
gate is released. `Completion_is_signalled_so_consumers_stop_after_the_last_
event` submits a known count of events across three consumers and asserts
`CompleteAndDrainAsync` returns with that exact count processed, proving
consumers stop cleanly once the writer is completed rather than hanging.
`A_cancelled_submit_token_is_honoured` passes a pre-cancelled token into
`SubmitAsync` and asserts it throws `OperationCanceledException` instead of
writing.

No `Thread.Sleep`, no `Task.Delay`, no wall-clock read, and no timeout-based
`Task.WhenAny` anywhere in this example, in the library or the tests. Every
test drives the pipeline with known event counts and `Task.WhenAll`, and the
one test that needs to observe an in-flight state uses a
`TaskCompletionSource<bool>` gate instead of a race against the clock.

## Build and test

```sh
cd examples/36-producer-consumer-and-pipelines
dotnet test --solution HandlingEventIngest.slnx
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
