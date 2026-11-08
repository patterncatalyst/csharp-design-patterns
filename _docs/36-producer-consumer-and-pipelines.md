---
title: "Producer-Consumer and pipelines as patterns"
order: 36
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "Producer-Consumer at the pattern level, with the Channels mechanics left to the concurrency reference."
---

<!-- EXAMPLE (shipping domain): A handling-event ingest pipeline. -->

## Learning objectives

By the end of this chapter you can:

- Build a Producer-Consumer pipeline over a bounded channel where many producers and many consumers share one queue, with every event consumed exactly once.
- Explain why one producer's order survives one consumer, and why that guarantee goes no further.
- Show backpressure: a write to a full bounded channel waits instead of growing a buffer.
- Signal completion by closing the write side and awaiting every consumer task, with no wall-clock wait.
- Recognize when one stage should become a multi-stage pipeline, and how backpressure propagates upstream.

## Why this matters

A shipping network has no single source of handling events. Terminal scanners report a container received, stevedores report it loaded, customs brokers report clearance, a consignee reports a claim. Each reporter runs on its own schedule and connection, and all of them write into one ingest system at once. The ingest system must hand each event to a worker that records it, never lose or double-record one, and not make every reporter wait on every other when a ship arrives or a customs backlog clears.

`HandlingEventIngest` is that system, trimmed down. `TrackingId` identifies a consignment, `HandlingKind` lists what happened (`Receive`, `Load`, `Unload`, `Customs`, `Claim`), and `HandlingEvent` carries a tracking identifier, a kind and the reporter's own sequence number. `HandlingEventPipeline` is the queue and the workers. It takes a capacity, a consumer count and an optional per-event hook, exposes `SubmitAsync` for producers and `CompleteAndDrainAsync` for the caller, and returns an `IngestReport` with the processed total, the events each consumer observed, and counts by `HandlingKind`.

{% include excalidraw.html name="36-producer-consumer-and-pipelines" caption="Figure 36.1: Several producers write handling events into one bounded channel while several consumers drain it concurrently. A full channel slows the producers, and completing the writer once stops every consumer after the last event." %}

## Pattern: Producer-Consumer

### The problem

Reporters and recorders run at different paces and in different numbers. A direct call from reporter to worker ties the reporter's pace to the worker's, and makes each side know about the other.

### The solution

**Producer-Consumer has independent producers and independent consumers exchange work through a shared, synchronized queue instead of calling each other.**

### How C# models it

`HandlingEventPipeline`'s constructor builds a bounded channel from `System.Threading.Channels` sized to `capacity` and starts `consumerCount` consumer tasks. A producer only calls `SubmitAsync`, which writes to the channel. Each consumer is a private loop, `ConsumeAsync`, that runs `await foreach` over the read side, invokes `onEvent`, records the event and moves on.

Neither side knows the other. A reporter doesn't know whether one consumer or ten is draining; a consumer doesn't know which reporter sent an item. The channel is the only shared state, and making concurrent access to it safe is the channel's job. That lets producer and consumer counts vary independently.

A bounded channel adds a capacity limit. An unbounded queue would still be Producer-Consumer and would still lose nothing, but a slow consumer would become silent memory growth. The bound makes that condition visible.

### Every event is consumed exactly once

Four producers each submit 250 events into a pipeline with capacity 8 and four consumers. After the producers finish and the pipeline drains, the multiset of observed events equals the multiset produced, on every run, however the scheduler interleaves the writes and reads.

Two things make that hold:

- the channel delivers each item to exactly one reader, never to all of them and never to two;
- each consumer records into a shared `ConcurrentQueue<HandlingEvent>` and bumps a shared counter with `Interlocked.Increment`, so the bookkeeping is safe under concurrent consumers with no explicit lock in `HandlingEventPipeline`.

{% include excalidraw.html name="36-exactly-once-drain" caption="Figure 36.2: Submitting a known number of events, completing the writer, and waiting for every consumer task to finish proves each event is recorded exactly once, and a single producer's order survives a single consumer because the channel is first in, first out." %}

### Order holds only for one producer and one consumer

A channel is first in, first out. One producer submitting 100 events with increasing `Sequence` values into a one-consumer pipeline gets them recorded in that order, with no gaps.

The guarantee stops there:

| Configuration | Order the consumers see |
| --- | --- |
| One producer, one consumer | The producer's order |
| Several producers | Each producer's events may interleave arbitrarily with the others' |
| Several consumers | Each consumer sees an ordered subset; none sees the global sequence |

Producer-Consumer promises no loss and no duplication, not a global order. A stage that needs global order needs one consumer, or partitioning so everything that must stay in relative order lands on the same consumer. That's a design decision above the queue.

### Backpressure: the producer's write waits

The channel uses `BoundedChannelFullMode.Wait`: a write to a full channel neither fails nor drops an item. It waits. With capacity 1 and one consumer whose `onEvent` hook blocks on a `TaskCompletionSource` gate:

1. The first event is picked up by the consumer, which empties the channel, and the consumer parks in the gate.
2. The second event fills the channel.
3. The third `SubmitAsync` returns a pending `ValueTask` that doesn't complete while the gate is closed.
4. Opening the gate lets the consumer read, and the third submission completes.

The producer feels the slow consumer directly, as a write that doesn't resolve. The full modes trade differently:

| `BoundedChannelFullMode` | On a full channel | Keeps every event |
| --- | --- | --- |
| `Wait` | Producer waits for room | Yes |
| `DropNewest`, `DropOldest` | An item is discarded; the producer never blocks | No |

An ingest pipeline that can't lose a handling event uses `Wait`, the only mode that keeps exactly-once under load.

### Completion without a wall-clock wait

`CompleteAndDrainAsync` works in two steps. `Writer.Complete()` tells every consumer's `await foreach` over `ReadAllAsync()` that no more items are coming after the buffered ones. Then `Task.WhenAll` over the consumer tasks resolves once each has left its loop. Only then is the `IngestReport` built. Sixty events across three consumers report 60 processed the moment `CompleteAndDrainAsync` returns.

Nothing uses a sleep, a fixed delay or a timeout race to decide whether work is done. A wait-then-check would measure how fast the scheduler ran that time. `Writer.Complete()` plus `Task.WhenAll` is deterministic: the pipeline is done when every consumer has returned, which happens when `ReadAllAsync()` has seen both the completion signal and an empty buffer.

### Cancellation on submission

`SubmitAsync` null-checks the event and forwards its `CancellationToken` to the channel's `WriteAsync`. A pre-cancelled token throws `OperationCanceledException`, and the drained pipeline reports 0 processed, so the event wasn't written anyway. A write waiting on a full channel honors the same token: cancelling it ends the wait with an exception. A producer that times out or shuts down mid-submission can stop waiting without leaving it unclear whether the event landed.

### Pipelines: chaining stages over bounded channels

A consumer can also be a producer, writing the event, or something derived from it, onto a second bounded channel feeding a second stage. Receive raw reports, validate them against known tracking identifiers, record validated events to storage: each stage consumes from the channel feeding it and produces onto the next. Every link uses the same bounded channel, per-stage consumer count and completion signal.

Chaining makes backpressure travel. If validate is slower than receive, the channel between them fills, receive's writes wait, and if receive is fed by another stage, that stage's writes wait too, one full channel at a time, back to the source. A slow stage slows the stages ahead of it instead of letting them pile up work it can't absorb. Each stage is tuned on its own: a stage doing network or disk work gets more consumers than one that only checks a shape.

{% include excalidraw.html name="36-pipeline-stages" caption="Figure 36.3: Receive, validate, and record each run as their own producer and consumer pair, linked by bounded channels. When validate slows down, its full channel propagates backpressure upstream to receive." %}

## Build, run, observe

```
cd examples/36-producer-consumer-and-pipelines && dotnet test
```

All 5 tests pass: exactly-once consumption under multi-producer, multi-consumer contention, single-producer order, completion through `Writer.Complete()` and `Task.WhenAll`, a cancelled submission, and backpressure on a full capacity-1 channel. None sleeps, delays or races a timeout.

## When not to use it

Producer-Consumer pays when the two sides run at different paces or in different numbers.

- **A direct call suffices.** If the producer can call the work and get an answer before it needs to do anything else, a channel, a consumer count and a drain protocol add background tasks and maintenance for no decoupling benefit.
- **Global order across many producers or consumers.** The queue doesn't provide it. Use one consumer for events that must stay ordered, or partition so related events always reach the same consumer.
- **An unbounded queue instead of a bounded one.** It hides a slow consumer as memory growth until an out-of-memory failure turns it into an outage. `BoundedChannelFullMode.Wait` turns the same problem into a visibly waiting producer while it's still a performance issue.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a pipeline.** "Add a queue between ingest and processing" leaves open its bound, how many consumers read it and how shutdown drains it. State them:

> In `HandlingEventPipeline`, create the channel with `Channel.CreateBounded<HandlingEvent>` and `BoundedChannelFullMode.Wait`, never `CreateUnbounded`. Start `consumerCount` `ConsumeAsync` tasks in the constructor, each reading with `ReadAllAsync`. `SubmitAsync` returns the writer's `WriteAsync(e, ct)`, so a full channel makes the producer wait and a cancelled token throws. `CompleteAndDrainAsync` calls `Writer.Complete()`, awaits `Task.WhenAll` over the consumers, and only then builds the `IngestReport`. Don't add sleeps or timeouts.

Run `dotnet test --project HandlingEventIngest.Tests --filter "PipelineTests|BackpressureTests"`; all 5 must pass.
</blockquote>

## For deeper coverage

- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue entry for Producer-Consumer, independent producers and consumers exchanging work through a shared, synchronized queue.
- Microsoft Learn, [Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels): the authority for bounded and unbounded channel creation, `BoundedChannelFullMode`, and the producer and consumer APIs `HandlingEventPipeline` builds on.
- Microsoft Learn, [Channel\<T\> class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1): the API reference for `Writer.WriteAsync`, `Writer.Complete`, and `Reader.ReadAllAsync`.
- Microsoft Learn, [Task.WhenAll method](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall): the coordination primitive behind `CompleteAndDrainAsync`'s deterministic wait for every consumer task.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
